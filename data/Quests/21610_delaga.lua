
-- [기사단서기관]델라가 

-- EVENT 는 100번 이상 부터 사용하라

-- UID : 서버에서 제공하는 유저번호
-- EVENT : 서버에서 제공하는 퀘스트 번호
-- STEP : 서버에서 제공하는 퀘스트 내부 단계

-- 위의 세가지 파라메타는 루아 실행시 항상 전역변수로 제공?

-- 지역변수 선언...
local UserClass;
local QuestNum;
local Ret = 0;
local NPC = 21610
local savenum = -1


-- [기사단서기관]델라가  클릭시 퀘스트 체크  


if EVENT == 240 then
   SelectMsg(UID, 3, savenum, 4150, NPC, 4150, 280, 4151, 360, 4155, 370, 4156, 380, 4154, 241);
end

if EVENT == 241 then
	Ret = 1;
end

-----------------------------------------------------------
-- 기사단 승격 시작 
-----------------------------------------------------------
local Knight
local ClanGrade
local Chief


if EVENT == 280 then
Knight = CheckKnight(UID)
    if Knight == 1 then -- 클랜 
    ClanGrade = CheckClanGrade(UID)
        if ClanGrade < 4 then -- 클랜 3등급 이상
        Chief = isClanLeader(UID)
            if Chief == true then -- 클랜단장일때 
            Chief = isClanLeader(UID)
                SelectMsg(UID, 3, savenum, 4165, NPC, 4157, 281, 4158, 286, 4159, 285);
            else-- 클랜단장 아닐때
            SelectMsg(UID, 2, savenum, 4164, NPC, 10, 241);
            end
        else-- 클랜 3등급 이상 아닐때
        SelectMsg(UID, 2, savenum, 4163, NPC, 10, 241);
        end
    else-- 클랜 아닐때
    SelectMsg(UID, 2, savenum, 4162, NPC, 10, 241);
    end
end

if EVENT == 281 then
    SelectMsg(UID, 2, savenum, 4166, NPC, 4160, 282, 27, 241);
end

if EVENT == 282 then
    SelectMsg(UID, 2, savenum, 4167, NPC, 4160, 283, 27, 241);
end

if EVENT == 283 then
    SelectMsg(UID, 2, savenum, 4168, NPC, 27, 241, -1, -1,  -1, -1);
end

if EVENT == 285 then
-- 명령어 나오면 그걸로 수정해야 될부분
ZoneChangeClan(UID, 54, 150, 150) -- 수정했음
end


local ItemA
local ItemB


if EVENT == 286 then
    SelectMsg(UID, 4, 415, 4172, NPC, 4161, 287, 4162, 241);
end


if EVENT == 287 then
    ItemA = HowmuchItem(UID, 900000000);  --노아
    ItemB = HowmuchItem(UID, 910045000);  -- 켄타목 
    if ItemA < 10000000 then -- 노아
    SelectMsg(UID, 2, savenum, 4170, NPC, 10, 241);
    elseif ItemB == 0 then
    SelectMsg(UID, 2, savenum, 4171, NPC, 10, 241);
    else-- ok
    RunExchange(UID, 467);
    PromoteKnight(UID)
    end
end

-----------------------------------------------------------
-- 기사단 승격 끝 
-----------------------------------------------------------


local Loyalty
-----------------------------------------------------------
-- 기본 국가 기여도 시작
-----------------------------------------------------------

if EVENT == 360 then
Loyalty = CheckLoyalty(UID)
	  if  Loyalty > 100  then -- 기여도가 100이상일때 
	   SelectMsg(UID, 2, savenum, 4155, NPC, 10, 241);
	  else-- 기여도가 100이하로 충전 가능할때 
      SelectMsg(UID, 2, savenum, 4156, NPC, 4152, 361, 4153, 363, 4154, 241);
	  end
end

if EVENT == 361 then --500충전 
ITEM_COUNT = HowmuchItem(UID, 900000000);
    if ITEM_COUNT < 1500000 then --노아가 1500000없을때
    SelectMsg(UID, 2, savenum, 4160, NPC, 10, 241);
	  else-- 기여도 충전 가능할때 
    SelectMsg(UID, 4, 413, 4157, NPC, 22, 362, 23, 241);
    end
end


if EVENT == 362 then --수락 
    RunExchange(UID, 465);
 end

if EVENT == 363 then --100충전 
ITEM_COUNT = HowmuchItem(UID, 900000000);
    if ITEM_COUNT < 350000 then --노아가 350000없을때
    SelectMsg(UID, 2, savenum, 4161, NPC, 10, 241);
	  else-- 기여도 충전 가능할때 
    SelectMsg(UID, 4, 414, 4159, NPC, 22, 364, 23, 241);
    end
end

if EVENT == 364 then --수락 
    RunExchange(UID, 466);
 end

-----------------------------------------------------------
-- 기본 국가 기여도 끝 ?
-----------------------------------------------------------



-- 0: 목록에 없음
-- 1: 녹봉지급
-- 2: 이미 받음

local Reward

-----------------------------------------------------------
-- 월간 기사 녹봉 시작
-----------------------------------------------------------

if EVENT == 370 then
Reward = RequestPersonalRankReward(UID)
    if  Reward == 0 then -- 목록에 없음
    SelectMsg(UID, 2, savenum, 4153, NPC, 10, 241);
    elseif Reward == 2 then-- 이미 받음
    SelectMsg(UID, 2, savenum, 4154, NPC, 10, 241);
    else-- 녹봉지급
    Ret = 1; 
    end
end

-----------------------------------------------------------
-- 월간 기사 녹봉 끝 ?
-----------------------------------------------------------

-----------------------------------------------------------
-- 전리품 전달 시작
-----------------------------------------------------------

if EVENT == 470 then
Reward = MonthPersonalRankRequestReward(UID)
    if  Reward == 0 then -- 목록에 없음
    SelectMsg(UID, 2, savenum, 4802, NPC, 10, 241);
    elseif Reward == 2 then-- 이미 받음
    SelectMsg(UID, 2, savenum, 4803, NPC, 10, 241);
    else-- 녹봉지급
    Ret = 1; 
    end
end

-----------------------------------------------------------
-- 전리품 전달 끝 
-----------------------------------------------------------


-----------------------------------------------------------
-- 국가 기사의 녹봉 시작
-----------------------------------------------------------
local Reward;

if EVENT == 380 then
Reward = RequestReward(UID)
    if  Reward == 0 then -- 목록에 없음
    SelectMsg(UID, 2, savenum, 4151, NPC, 10, 241);
    elseif Reward == 2 then-- 이미 받음
    SelectMsg(UID, 2, savenum, 4152, NPC, 10, 241);
    else-- 녹봉지급
    Ret = 1; 
    end
end
-----------------------------------------------------------
-- 국가 기사의 녹봉 끝 ?
-----------------------------------------------------------

return Ret;