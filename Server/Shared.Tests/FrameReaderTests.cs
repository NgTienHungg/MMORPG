using System.Text;
using MMORPG.Shared.Net;

namespace MMORPG.Shared.Tests
{
    public class FrameReaderTests
    {
        private static byte[] Frame(int cmd, string text) =>
            PacketFrame.Encode(cmd, Encoding.UTF8.GetBytes(text));

        [Fact]
        public void DocDuocGoiNguyenVen()
        {
            var reader = new FrameReader();
            byte[] frame = Frame(7, "hello");

            reader.Feed(frame, 0, frame.Length);

            Assert.True(reader.TryRead(out int cmd, out byte[] payload));
            Assert.Equal(7, cmd);
            Assert.Equal("hello", Encoding.UTF8.GetString(payload));
            Assert.False(reader.TryRead(out _, out _));
        }

        [Fact]
        public void GoiBiCatLamNhieuLan_VanGhepDuoc()
        {
            var reader = new FrameReader();
            byte[] frame = Frame(7, "hello");

            // mô phỏng TCP cắt gói: đưa vào từng byte một
            for (int i = 0; i < frame.Length - 1; i++)
            {
                reader.Feed(frame, i, 1);
                Assert.False(reader.TryRead(out _, out _)); // chưa đủ thì không được trả gói
            }

            reader.Feed(frame, frame.Length - 1, 1);
            Assert.True(reader.TryRead(out int cmd, out byte[] payload));
            Assert.Equal("hello", Encoding.UTF8.GetString(payload));
        }

        [Fact]
        public void NhieuGoiDinhLien_TachDuocHet()
        {
            var reader = new FrameReader();
            byte[] a = Frame(1, "one");
            byte[] b = Frame(2, "two");
            byte[] merged = new byte[a.Length + b.Length];
            a.CopyTo(merged, 0);
            b.CopyTo(merged, a.Length);

            reader.Feed(merged, 0, merged.Length);

            Assert.True(reader.TryRead(out int cmd1, out byte[] p1));
            Assert.True(reader.TryRead(out int cmd2, out byte[] p2));
            Assert.False(reader.TryRead(out _, out _));

            Assert.Equal(1, cmd1);
            Assert.Equal("one", Encoding.UTF8.GetString(p1));
            Assert.Equal(2, cmd2);
            Assert.Equal("two", Encoding.UTF8.GetString(p2));
        }

        [Fact]
        public void PayloadRong_VanHopLe()
        {
            var reader = new FrameReader();
            byte[] frame = PacketFrame.Encode(99, System.ReadOnlySpan<byte>.Empty);

            reader.Feed(frame, 0, frame.Length);

            Assert.True(reader.TryRead(out int cmd, out byte[] payload));
            Assert.Equal(99, cmd);
            Assert.Empty(payload);
        }

        [Fact]
        public void DoDaiVoLy_ThiNem()
        {
            var reader = new FrameReader();
            byte[] rac = { 0xFF, 0xFF, 0xFF, 0x7F }; // ~2GB

            reader.Feed(rac, 0, rac.Length);

            Assert.Throws<System.IO.InvalidDataException>(() => reader.TryRead(out _, out _));
        }
    }
}
