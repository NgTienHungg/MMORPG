using System;
using MemoryPack;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Bộ số của một lớp nhân vật. Ba vai, một hình dạng: dòng trong characters.json, phần tử của gói
    /// EnterWorld, và bộ số MovementRules.Step đọc.
    ///
    /// Property có setter là cái giá của việc tuần tự hoá được — kiểu bất biến thì không bộ tuần tự
    /// hoá nào dựng được nó. Bù lại bằng kỷ luật, không bằng trình biên dịch: CHỈ
    /// <see cref="CharacterConfigContainer.Load"/> được ghi vào, và nó chỉ ghi vào object vừa dựng xong,
    /// chưa ai cầm.
    /// </summary>
    [MemoryPackable]
    public sealed partial class CharacterConfig
    {
        public int ClassId { get; set; }

        /// <summary>Tên để đọc log và sửa file cho dễ. Mô phỏng không dùng, nên nó cũng không vào Checksum.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Tốc độ chạy ngang, world unit/giây.</summary>
        public float MoveSpeed { get; set; } = 5f;

        /// <summary>Vận tốc bật lên tức thời khi nhảy.</summary>
        public float JumpSpeed { get; set; } = 16f;

        /// <summary>Nửa bề ngang thân. Hẹp hơn nửa ô để lọt vừa khe rộng đúng 1 ô.</summary>
        public float BodyHalfWidth { get; set; } = 0.35f;

        /// <summary>Chiều cao thân khi đứng. Gốc toạ độ ở CHÂN nên thân chiếm [Y, Y + cao].</summary>
        public float BodyHeight { get; set; } = 1.6f;

        /// <summary>Chiều cao khi ngồi — thấp hơn 1 ô nên chui được vào khe cao đúng một ô.</summary>
        public float BodyHeightCrouch { get; set; } = 0.9f;

        /// <summary>Mảng chứ không phải object có khoá cố định: thêm một hành động mới là thêm phần tử.</summary>
        public ActionData[] Actions { get; set; } = Array.Empty<ActionData>();

        /// <summary>
        /// Quy giây ra tick cho mọi hành động. Gọi từ <see cref="CharacterConfigContainer.Load"/>, tức là
        /// đúng một chỗ ở mỗi bên — cùng cơ chế với <see cref="WorldRules.Prepare"/>.
        /// </summary>
        public void Prepare()
        {
            // for chứ không foreach: ActionData là struct, foreach cho ra BẢN COPY và Prepare() sẽ ghi
            // vào bản copy ấy rồi vứt đi. Không lỗi, không cảnh báo, chỉ là mọi thời lượng bằng 0 —
            // đúng mặt trái của tính chất "gán là copy" đã cứu vòng replay ở Phase 8.
            for (int i = 0; i < Actions.Length; i++)
                Actions[i].Prepare();
        }

        /// <summary>
        /// Số liệu của một hành động. Hành động không có trong bảng (kể cả None) trả về bản rỗng:
        /// 0 tick, không khoá thân — nhờ vậy chỗ gọi không phải kiểm null hay kiểm None.
        ///
        /// Quét thẳng thay vì Dictionary: bảng có ba dòng, và ba phép so bằng trên một mảng liền kề
        /// nhanh hơn một phép băm. Đổi khi nào một lớp nhân vật có vài chục chiêu.
        /// </summary>
        public ActionData GetAction(ActionState action)
        {
            for (int i = 0; i < Actions.Length; i++)
            {
                if (Actions[i].Action == action)
                    return Actions[i];
            }

            return default;
        }
    }
}
