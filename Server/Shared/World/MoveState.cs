using System.Runtime.InteropServices;

namespace MMORPG.Shared.World
{
    /// <summary>
    /// Ý định của người chơi tại một tick — đúng những gì bấm được trên bàn phím, không hơn.
    /// Cố tình không có trục Y: trong platformer người chơi không điều khiển chiều dọc,
    /// chiều dọc là hệ quả của trọng lực và của cú nhảy.
    ///
    /// Đi thẳng trên dây (bọc trong MoveInputRequest) nên mọi field ở đây đều là thứ kẻ lạ điều
    /// khiển được: bên nhận phải kiểm, không được tin.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MoveIntent
    {
        /// <summary>Hướng ngang trong [-1, 1]. Người gọi chịu trách nhiệm kẹp; hàm Step không kiểm lại.</summary>
        public float DirX;

        /// <summary>
        /// CẠNH LÊN của nút nhảy: "tại tick này người chơi vừa bấm", không phải "nút đang bị giữ".
        /// Cạnh chứ không phải mức, vì ở 20 tick/s một cú bấm nhanh 30ms sẽ lọt trọn vào khe giữa
        /// hai lần đọc mức — người chơi bấm mà nhân vật không nhảy, không có lỗi nào để lần theo.
        /// </summary>
        public bool Jump;

        /// <summary>
        /// MỨC của nút ngồi: bấm là ngồi, thả là đứng. Ngược hẳn với Jump — ngồi là một tư thế kéo
        /// dài nên chỉ giá trị mới nhất có nghĩa, gộp lại là ngồi mãi không đứng dậy được.
        /// </summary>
        public bool Crouch;

        /// <summary>
        /// Hành động vừa xin, dạng CẠNH như Jump. Kiểu ActionRequest chứ không phải ActionState:
        /// tập giá trị này cố tình hẹp hơn, để "xin được Hurt" là câu không viết ra được.
        /// </summary>
        public ActionRequest Action;
    }

    /// <summary>
    /// Toàn bộ trạng thái của một entity — tập nhỏ nhất mà biết nó thì tính được tick kế tiếp,
    /// VÀ vẽ được nhân vật ra màn hình. Hai vai trò trong một struct là có chủ đích: nhờ vậy hoạt
    /// ảnh được reconciliation lo hộ, không cần một đường đồng bộ riêng.
    ///
    /// Vận tốc nằm ở đây chứ không suy ra từ vị trí: hai nhân vật cùng một điểm, một đang bay lên
    /// một đang rơi xuống, tick sau sẽ ở hai chỗ khác nhau.
    ///
    /// Là struct field công khai chứ không phải class có property: nó bị copy hàng chục lần mỗi giây
    /// trong vòng replay của reconciliation, và tính "gán là copy" của value type chính là thứ giữ
    /// cho replay không vô tình sửa trạng thái gốc.
    /// </summary>
    /// <remarks>
    /// Struct này đi thẳng trên dây bên trong <c>MoveStateResponse</c>. Vì mọi field đều là kiểu
    /// unmanaged, MemoryPack không sinh mã đọc/ghi từng field mà **copy nguyên khối bộ nhớ** — nhanh,
    /// nhưng đổi lại byte trên dây chính là bố cục struct trong RAM. Ba ràng buộc đi kèm:
    /// (1) <see cref="LayoutKind.Sequential"/> ghi tường minh để server (CoreCLR) và client
    /// (Mono/IL2CPP) chắc chắn xếp field giống nhau; (2) thêm một field kiểu tham chiếu (string,
    /// mảng...) là phá tính unmanaged — lúc đó phải đổi cách gửi, không phải chỉ thêm một dòng;
    /// (3) mỗi lần thêm/bớt/đổi thứ tự field là một lần đổi giao thức, DLL cũ bên Unity sẽ đọc ra
    /// những con số vô nghĩa mà không báo lỗi.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential)]
    public struct MoveState
    {
        public float X;
        public float Y;
        public float VelX;
        public float VelY;

        /// <summary>Chân có đang chạm sàn ở CUỐI tick trước không. Dùng cho hiển thị; điều kiện nhảy dùng bộ đếm bên dưới.</summary>
        public bool Grounded;

        /// <summary>
        /// Số tick từ lần cuối chạm đất. 0 = đang đứng đất. Cho phép "coyote time": vẫn nhảy được vài
        /// tick sau khi đã rời mép sàn — người chơi luôn cảm thấy mình bấm kịp, và với họ thì họ đúng.
        /// </summary>
        public int TicksSinceGrounded;

        /// <summary>
        /// Số tick từ lần cuối bấm nhảy. Cho phép "jump buffer": bấm sớm ngay trước lúc tiếp đất thì
        /// cú bấm được giữ lại, chạm đất là bật lên ngay thay vì rơi vào hư không.
        /// </summary>
        public int TicksSinceJumpRequest;

        /// <summary>
        /// Đang ngồi. Là sự thật vật lý chứ không phải chuyện hình ảnh: ngồi thì VelX bị ép về 0,
        /// nên nó phải nằm trong trạng thái mà cả hai bên cùng mô phỏng.
        /// </summary>
        public bool Crouching;

        /// <summary>
        /// Hướng mặt. Không suy ra được từ VelX vì nó có TRÍ NHỚ — đứng yên thì giữ hướng cũ.
        /// Và vì hướng quyết định đòn đánh nhắm về phía nào nên nó phải do server chốt.
        /// </summary>
        public bool FacingLeft;

        /// <summary>Hành động đang thực hiện. Chỉ Step (từ ý định) hoặc lệnh của server đặt được.</summary>
        public ActionState Action;

        /// <summary>Số tick còn lại của hành động. Đếm ngược trong Step nên replay tự đúng.</summary>
        public int ActionTicksLeft;

        /// <summary>Số tick kể từ lần đánh gần nhất — nền của cooldown. Cùng họ với TicksSinceGrounded.</summary>
        public int TicksSinceAttack;

        /// <summary>
        /// Trạng thái lúc mới vào world. Grounded = false có chủ ý: để tick đầu tiên tự rơi và tự
        /// phát hiện sàn, thay vì tin rằng toạ độ lấy từ DB đang đứng đúng trên mặt đất.
        /// </summary>
        public static MoveState AtRest(float x, float y)
        {
            return new MoveState
            {
                X = x, Y = y, VelX = 0f, VelY = 0f,
                Grounded = false,
                TicksSinceGrounded = MovementRules.EXPIRED, // Bắt đầu ở trạng thái hết hạn: vừa vào world thì chưa có tư cách nhảy nào cả.
                TicksSinceJumpRequest = MovementRules.EXPIRED,
                Crouching = false,
                FacingLeft = false,
                Action = ActionState.None,
                ActionTicksLeft = 0,
                TicksSinceAttack = MovementRules.EXPIRED, // Hết cooldown sẵn: vừa vào world là đánh được ngay.
            };
        }
    }
}