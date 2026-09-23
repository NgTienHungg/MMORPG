using MMORPG.Shared.World;
using MMORPG.Shared.World.Character;
using MMORPG.Shared.World.Item;

namespace MMORPG.Shared.Tests
{
    /// <summary>
    /// Vân tay bảng config là thứ DUY NHẤT phát hiện hai bên chạy hai bộ dữ liệu khác nhau — nên nó
    /// phải nhạy đúng chỗ và trơ đúng chỗ. Ba tính chất dưới đây là ba cách nó hỏng câm được.
    /// </summary>
    public class ConfigFingerprintTests
    {
        private static ItemTableData SampleItems()
        {
            return new ItemTableData
            {
                Version = 1,
                Items = new[]
                {
                    new ItemConfig { TemplateId = 1, Name = "Bình máu nhỏ", Kind = ItemKind.Consumable, MaxStack = 20 },
                    new ItemConfig { TemplateId = 100, Name = "Kiếm gỗ", Kind = ItemKind.Equipment, MaxStack = 1 },
                },
            };
        }

        [Fact]
        public void Of_SameContent_SameFingerprint()
        {
            // Nếu tính chất này hỏng thì mọi lần vào world đều báo lệch, kể cả khi hai bên khớp.
            Assert.Equal(ConfigFingerprint.Of(SampleItems()), ConfigFingerprint.Of(SampleItems()));
        }

        [Fact]
        public void Of_ChangedValue_DifferentFingerprint()
        {
            ItemTableData changed = SampleItems();
            changed.Items[0].MaxStack = 99;

            Assert.NotEqual(ConfigFingerprint.Of(SampleItems()), ConfigFingerprint.Of(changed));
        }

        [Fact]
        public void Of_ChangedVersion_DifferentFingerprint()
        {
            ItemTableData changed = SampleItems();
            changed.Version = 2;

            Assert.NotEqual(ConfigFingerprint.Of(SampleItems()), ConfigFingerprint.Of(changed));
        }

        [Fact]
        public void Of_ReorderedRows_DifferentFingerprint()
        {
            // Đảo thứ tự dòng là đảo thứ tự byte. Băm cộng dồn từng trường rời thì hai bảng hoán vị
            // ra cùng số — đó là lý do Fnv1a nuốt từng byte thay vì cộng thẳng giá trị vào.
            ItemTableData reordered = SampleItems();
            (reordered.Items[0], reordered.Items[1]) = (reordered.Items[1], reordered.Items[0]);

            Assert.NotEqual(ConfigFingerprint.Of(SampleItems()), ConfigFingerprint.Of(reordered));
        }

        /// <summary>
        /// Ghim một hành vi của MemoryPack mà đoán sai thì hỏng câm: <b>struct chỉ chứa kiểu
        /// unmanaged bị chép nguyên khối, mọi thuộc tính trên từng trường đều vô hiệu.</b>
        ///
        /// Vì thế <c>ActionData.DurationTicks</c> — thứ <c>Prepare()</c> tính ra — vẫn nằm trong byte
        /// tuần tự hoá, và hai bên bắt buộc phải băm ở CÙNG một thời điểm: cả hai đều băm sau
        /// <c>load()</c>.
        /// </summary>
        [Fact]
        public void Of_BlittableStruct_IncludesDerivedFields()
        {
            CharacterTableData table = SampleCharacters();
            uint beforePrepare = ConfigFingerprint.Of(table);

            foreach (CharacterConfig config in table.Classes)
                config.Prepare();

            Assert.True(table.Classes[0].Actions[0].DurationTicks > 0,
                "Prepare() phải quy ra tick, nếu không phép kiểm này vô nghĩa");

            Assert.NotEqual(beforePrepare, ConfigFingerprint.Of(table));
        }

        [Fact]
        public void Of_AfterPrepare_IsStable()
        {
            // Thứ hai bên thật sự so với nhau: vân tay của bảng ĐÃ nạp xong. Nó phải ổn định.
            CharacterTableData first = SampleCharacters();
            CharacterTableData second = SampleCharacters();

            foreach (CharacterConfig config in first.Classes)
                config.Prepare();

            foreach (CharacterConfig config in second.Classes)
                config.Prepare();

            Assert.Equal(ConfigFingerprint.Of(first), ConfigFingerprint.Of(second));
        }

        private static CharacterTableData SampleCharacters()
        {
            return new CharacterTableData
            {
                Version = 1,
                Classes = new[]
                {
                    new CharacterConfig
                    {
                        ClassId = 1,
                        Name = "Dragon Warrior",
                        Actions = new[]
                        {
                            new ActionData { Action = ActionState.Attack, DurationSeconds = 0.25f, CooldownSeconds = 0.4f },
                        },
                    },
                },
            };
        }
    }
}
