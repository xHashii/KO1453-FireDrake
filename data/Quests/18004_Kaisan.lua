
-- [대상인] 카이샨
-- 그냥 닫기 101

-- EVENT 는 100번 이상 부터 사용하라

-- UID : 서버에서 제공하는 유저번호
-- EVENT : 서버에서 제공하는 퀘스트 번호
-- STEP : 서버에서 제공하는 퀘스트 내부 단계

-- 위의 세가지 파라메타는 루아 실행시 항상 전역변수로 제공?

-- 지역변수 선언...
local UserClass;
local QuestNum;
local Ret = 0;
local NPC=18004; --NPC 번호 


-- [대상인] 카이샨 클릭시 퀘스트 체크  

if EVENT == 100 then
          NpcMsg(UID, 147, NPC)
end

if EVENT == 101 then -- 그냥 닫기 
    Ret = 1;
end


-----------------------------------------------------------
-- 1차 전직 시작
-----------------------------------------------------------
local savenum = 406;

-- 시드미셀 등장

if EVENT == 400 then
   Class = CheckClass(UID);
   if Class == 1 or Class == 2 or Class == 3 or Class == 4 then -- 1차 전직 전인가 체크
      SaveEvent(UID, 4061);
      NATION = CheckNation(UID);
      if NATION == 1 then --카루스 일때 
      SelectMsg(UID, 1, savenum, 4062, NPC, 4061, 101);
      else -- 엘모일때
      SelectMsg(UID, 1, savenum, 4063, NPC, 4061, 101);
      end
   else
   Ret = 1;
   end
end


-- [대상인] 카이샨 100번의 속성 0번 4번일 경우

if EVENT == 403 then   
Level = CheckLevel(UID)
   if Level > 9 then -- 레벨이 9이상인가?   
   Class = CheckClass(UID);
      if Class == 1 or Class == 2 or Class == 3 or Class == 4 then -- 1차 전직 전인가 체크
       ITEM_COUNT = HowmuchItem(UID, 900000000);
         if ITEM_COUNT < 3000 then --노아가 3000없을때 
         SelectMsg(UID, 2, savenum, 4065, NPC, 10, 101);
         else -- 3000노아가 있을때
         SelectMsg(UID, 2, savenum, 4064, NPC, 4062, 404, 4063, 101);
         end
      else --1차 전직을 이미 했을때
      SelectMsg(UID, 2, savenum, 4068, NPC, 10, 101);
      end
   else --레벨이 10이하일때 
   SelectMsg(UID, 2, savenum, 4068, NPC, 10, 101);
   end
end

if EVENT == 404 then
   SaveEvent(UID, 4063);
   PromoteUserNovice(UID)
   RobItem(UID, 900000000, 3000);
   NATION = CheckNation(UID);
   if NATION == 1 then --카루스 일때 
   SelectMsg(UID, 1, savenum, 4066, NPC, 4064, 101);
   else -- 엘모일때
   SelectMsg(UID, 1, savenum, 4067, NPC, 4064, 101);
   end
end


-----------------------------------------------------------
-- 1차 전직 끝 
-----------------------------------------------------------

-----------------------------------------------------------
-- 재분배 시작
-----------------------------------------------------------

local savenum = 407;

if EVENT == 407 then
   SaveEvent(UID, 4070);
   SelectMsg(UID, 2, savenum, 4070, NPC, 4070, 408, 10, 101);
end

if EVENT == 408 then
	SendStatSkillDistribute(UID)
end

-----------------------------------------------------------
-- 재분배 끝
-----------------------------------------------------------


-----------------------------------------------------------
-- 펫 시작
-----------------------------------------------------------

local savenum = 453;


if EVENT == 500 then
   SelectMsg(UID, 3, savenum, 4704, NPC, 4238, 501, 4239, 502, 4240, 503);
end


if EVENT == 501 then
   SelectMsg(UID, 9, savenum, 4704, NPC, 4070, 408, 10, 101);
end

if EVENT == 502 then
   SelectMsg(UID, 9, savenum, 4704, NPC, -1, -1);
end

if EVENT == 503 then
   SelectMsg(UID, 9, savenum, 4704, NPC, -1, -1);
end


-----------------------------------------------------------
-- 펫 끝
-----------------------------------------------------------
