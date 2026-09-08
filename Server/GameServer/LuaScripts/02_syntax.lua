local tui = { "kiếm", "khiên", "giáp" }
local item = { id = 1001, ten = "Bình Máu Nhỏ", gia = 50 }

local hp = 30

if hp <= 0 then
    print("da chet")
elseif hp < 50 then
    print("nguy kich")
else
    print("con khoe")
end

for i = 1, 3 do print("dem len:", i) end
for i = 10, 1, -2 do print("dem nguoc:", i) end

for index, giaTri in ipairs(tui) do print("o", index, "chua", giaTri) end
for khoa, giaTri in pairs(item) do print("khoa", khoa, "=", giaTri) end

local n = 0
while n < 3 do n = n + 1 end

for i = 1, 5 do
    if i % 2 == 1 then
        print("so le:", i)
    end
end
