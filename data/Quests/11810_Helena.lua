
-- 대사제 헬레나

-- EVENT 는 100번 이상 부터 사용하라

-- UID : 서버에서 제공하는 유저번호
-- EVENT : 서버에서 제공하는 퀘스트 번호
-- STEP : 서버에서 제공하는 퀘스트 내부 단계

-- 위의 세가지 파라메타는 루아 실행시 항상 전역변수로 제공?



-- Class  함수 만들어 주세요 (직업 체크하는 함수임)
-- 1:전사, 2:로그, 3:마법사, 4: 사제
-- 1차전직	5	7	9	11
-- 2차전직	6	8	10	12



-- SkillPoint 함수 만들어 주세요 ( 스킬 포인트 체크하는 함수임)
-- 1번스킬:5, 2번스킬 : 6, 	3번스킬:7  	마스터 : 8?


-- 지역변수 선언...
local Class;
local SkillPoint;
local QuestNum;
local Ret = 0;
local NPC = 11810
local Savenum = 400

-- 대사제 헬레나 클릭시 퀘스트 체크 

if EVENT == 4000 then
   Class = CheckClass (UID);
   if Class == 4 or Class == 11 or Class == 12 then -- 사제인가를 체크 
      SkillPoint = CheckSkillPoint(UID, 5);
    if SkillPoint > 59 then -- 치료스킬이 60이상인가 체크 
    SelectMsg(UID, 3, -1, 4001,  NPC, 4000, 4007, 4001, 4010, 4002, 4013, 4003, 4016, 47, 4002);
    elseif SkillPoint > 49 then -- 치료스킬이 50이상인가 체크 
    SelectMsg(UID, 3, -1, 4001,  NPC, 4000, 4007, 4001, 4010, 4002, 4013, 47, 4002);
    elseif SkillPoint > 39 then -- 치료스킬이 40이상인가 체크 
    SelectMsg(UID, 3, -1, 4001,  NPC, 4000, 4007, 4001, 4010, 47, 4002);
    elseif SkillPoint > 29 then -- 치료스킬이 30이상인가 체크 
    SelectMsg(UID, 3, -1, 4001,  NPC, 4000, 4007, 47, 4002);
    else -- 치료스킬이 29이하일때 
         EVENT = 4001
      end
   else --사제가 아닐때
   EVENT = 4001
   end
end

if EVENT == 4001 then -- 물약 제조못한다는 메시지
   SelectMsg(UID, 2, -1, 4000,  NPC, 10, 4002);
end

if EVENT == 4002 then
    Ret = 1;
end

-- 대사제 헬레나 400번의 속성 시드인 경우 

if EVENT == 4004 then
   SkillPoint = CheckSkillPoint(UID, 5);
   if SkillPoint > 29 then
    SaveEvent(UID, 4003);
   SelectMsg(UID, 1, Savenum, 4005, NPC, 29, 4005);
   else -- 치료스킬이 29이하일때 
    Ret = 1;
   end
end

if EVENT == 4005 then
   ShowMap(UID, 400);
end

---------------------------------------
-- 대사제 헬레나 생명의성수 제조시작 
---------------------------------------

   local min_count;
   local ItemA ;
   local ItemB ;
   local Check ;

if EVENT == 4007 then
    Check = CheckExchange(UID, 400)
    if  Check == true then
	min_count = GetMaxExchange(UID, 400); --exchange 테이블 인덱스값 
        if min_count == 0 then
        ItemA = HowmuchItem(UID, 389010000);  
        ItemB = HowmuchItem(UID, 379001000); 
            if  ItemA == 0  then -- 성수가 없을때
            SelectMsg(UID, 2, Savenum, 4010, NPC, 10, 4002);
            elseif ItemB == 0  then-- 기도문이 없을때?
            SelectMsg(UID, 2, Savenum, 4011, NPC, 10, 4002);
            else
            SelectMsg(UID, 2, Savenum, 4007, NPC, 10, 4002);
            end
        else
        SelectMsg(UID, 4, Savenum, 4006, NPC, 4004, 4008, 4005, 4002);
        end
    else
    Ret = 1; 
    end  
end


if EVENT == 4008 then
   min_count = GetMaxExchange(UID, 400);
 	RunCountExchange(UID, 400, min_count);	
end

---------------------------------------
-- 대사제 헬레나 생명의성수 제조끝
---------------------------------------

local Savenum = 410

---------------------------------------
-- 대사제 헬레나 사랑의 성수 제조시작 
---------------------------------------
   local min_count;
   local ItemA ;
   local ItemB ;
   local Check ;

if EVENT == 4010 then
    Check = CheckExchange(UID, 401)
    if  Check == true then
	min_count = GetMaxExchange(UID, 401); --exchange 테이블 인덱스값 
        if min_count == 0 then
        ItemA = HowmuchItem(UID, 389010000);  
        ItemB = HowmuchItem(UID, 379002000); 
            if  ItemA == 0  then -- 성수가 없을때
            SelectMsg(UID, 2, Savenum, 4010, NPC, 10, 4002);
            elseif ItemB == 0  then-- 기도문이 없을때?
            SelectMsg(UID, 2, Savenum, 4011, NPC, 10, 4002);
            else
            SelectMsg(UID, 2, Savenum, 4007, NPC, 10, 4002);
            end
        else
        SelectMsg(UID, 4, Savenum, 4006, NPC, 4004, 4011, 4005, 4002);
        end
    else
    Ret = 1; 
    end  
end


if EVENT == 4011 then
   min_count = GetMaxExchange(UID, 401);
    RunCountExchange(UID, 401, min_count);	
end

---------------------------------------
-- 대사제 헬레나 사랑의 성수 제조끝
---------------------------------------


local Savenum = 411

---------------------------------------
-- 대사제 헬레나 은총의 성수 제조시작 
---------------------------------------
   local min_count;
   local ItemA ;
   local ItemB ;
   local Check ;

if EVENT == 4013 then
    Check = CheckExchange(UID, 402)
    if  Check == true then
	min_count = GetMaxExchange(UID, 402); --exchange 테이블 인덱스값 
        if min_count == 0 then
        ItemA = HowmuchItem(UID, 389010000);  
        ItemB = HowmuchItem(UID, 379003000); 
            if  ItemA == 0  then -- 성수가 없을때
            SelectMsg(UID, 2, Savenum, 4010, NPC, 10, 4002);
            elseif ItemB == 0  then-- 기도문이 없을때?
            SelectMsg(UID, 2, Savenum, 4011, NPC, 10, 4002);
            else
            SelectMsg(UID, 2, Savenum, 4007, NPC, 10, 4002);
            end
        else
        SelectMsg(UID, 4, Savenum, 4006, NPC, 4004, 4014, 4005, 4002);
        end
    else
    Ret = 1; 
    end  
end


if EVENT == 4014 then
   min_count = GetMaxExchange(UID, 402);
    RunCountExchange(UID, 402, min_count);	
end

---------------------------------------
-- 대사제 헬레나 은총의 성수 제조끝
---------------------------------------


local Savenum = 412

---------------------------------------
-- 대사제 헬레나 은혜의 성수 제조시작 
---------------------------------------

   local min_count;
   local ItemA ;
   local ItemB ;
   local Check ;

if EVENT == 4016 then
    Check = CheckExchange(UID, 403)
    if  Check == true then
	min_count = GetMaxExchange(UID, 403); --exchange 테이블 인덱스값 
        if min_count == 0 then
        ItemA = HowmuchItem(UID, 389010000);  
        ItemB = HowmuchItem(UID, 379004000); 
            if  ItemA == 0  then -- 성수가 없을때
            SelectMsg(UID, 2, Savenum, 4010, NPC, 10, 4002);
            elseif ItemB == 0  then-- 기도문이 없을때?
            SelectMsg(UID, 2, Savenum, 4011, NPC, 10, 4002);
            else
            SelectMsg(UID, 2, Savenum, 4007, NPC, 10, 4002);
            end
        else
        SelectMsg(UID, 4, Savenum, 4006, NPC, 4004, 4017, 4005, 4002);
        end
    else
    Ret = 1; 
    end  
end


if EVENT == 4017 then
   min_count = GetMaxExchange(UID, 403);
    RunCountExchange(UID, 403, min_count);	
end

---------------------------------------
-- 대사제 헬레나 은혜의 성수 제조끝
---------------------------------------


return Ret;