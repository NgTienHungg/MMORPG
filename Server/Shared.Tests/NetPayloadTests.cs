using System.Linq;
using MMORPG.Shared.Dto;
using MMORPG.Shared.Net;
using Xunit;

namespace MMORPG.Shared.Tests;

public class NetPayloadTests
{
    [Fact]
    public void GoiNho_KhongNen_VanDungNguyenVen()
    {
        var dto = new EchoRequest { Message = "xin chào" };

        byte[] packed = NetPayload.Serialize(dto);
        var back = NetPayload.Deserialize<EchoRequest>(packed);

        Assert.Equal(0x00, packed[0]); // flag = không nén
        Assert.Equal("xin chào", back.Message);
    }

    [Fact]
    public void GoiLon_LapLai_ThiNenVaVanGiaiDuoc()
    {
        // chuỗi lặp → nén rất tốt, chắc chắn vượt ngưỡng 4KB
        var dto = new EchoRequest { Message = string.Concat(Enumerable.Repeat("abcdefgh", 2000)) };

        byte[] packed = NetPayload.Serialize(dto);
        var back = NetPayload.Deserialize<EchoRequest>(packed);

        Assert.Equal(0x01, packed[0]); // flag = LZ4
        Assert.True(packed.Length < dto.Message.Length / 2, "nén phải ăn thua rõ rệt");
        Assert.Equal(dto.Message, back.Message);
    }

    [Fact]
    public void FlagLa_ThiNem()
    {
        byte[] rac = { 0x7F, 1, 2, 3 };
        Assert.Throws<System.IO.InvalidDataException>(() => NetPayload.Deserialize<EchoRequest>(rac));
    }
}
