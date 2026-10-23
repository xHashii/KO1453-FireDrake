
-- [경비병] 패트릭
-- 그냥 닫기 168

-- EVENT 는 100번 이상 부터 사용하라

-- UID : 서버에서 제공하는 유저번호
-- EVENT : 서버에서 제공하는 퀘스트 번호
-- STEP : 서버에서 제공하는 퀘스트 내부 단계

-- 위의 세가지 파라메타는 루아 실행시 항상 전역변수로 제공?

-- 지역변수 선언...
local UserClass;
local QuestNum;
local NPC = 13013;
local Ret = 0;

-- [경비병] 패트릭 클릭시 퀘스트 체크  

if EVENT == 165 then
	QuestNum = SearchQuest(UID, NPC);
		if QuestNum == 0 then --해당 NPC에게 할수 있는 퀘스트가 0개 일때 
			 SelectMsg(UID, 2, -1, 166, 13013, 10, 168);
			 Ret = 1; -- 이건 왜 저장 시켜요? 그냥 RETURN은 안되나요?
		elseif QuestNum > 1 and QuestNum < 100 then--해당 NPC에게 할수 있는 퀘스트가 1개 일때 
          NpcMsg(UID, 167,NPC)
      else--해당 NPC에게 할수 있는 퀘스트가 1개 이상 일때 
          EVENT = QuestNum
		end
end

if EVENT == 168 then -- 그냥 닫기 
    Ret = 1;
end

local NATION = 0;--국가 체크 함수 만들어 주세요
--local SHOW_MAP; -- 지도 보여주눈 함수 만들어 주세요


------------------------------------------------------
-- 웜 퇴치 시작 
------------------------------------------------------
--시드, 미셀 호출 
if EVENT == 170 then
--  NATION = CheckNation(UID);
--  if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 101, 170, 13013, 24, 171);
end

if EVENT == 171 then
   ShowMap(UID, 4);
   SaveEvent(UID, 48);
end

if EVENT == 172 then
   SelectMsg(UID, 1, 101, 172, 13013, 24, 171);
end

-- 101번의 속성 0번 4번일 경우 


if EVENT == 175 then
   SelectMsg(UID, 2, 101, 175, 13013,  25, 176, 13, -1);
end

local MonsterSub = 0;

if EVENT == 176 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 101, 176, 13013, 22, 178, 23, 168);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 101, 173, 13013, 10, 168);
   end
end

if EVENT == 178 then
   SaveEvent(UID, 49);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 180 then
   SaveEvent(UID, 51);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 101, 181, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때 
      SelectMsg(UID, 1, 101, 182, 13013, 14, 168);
   end
end

-- 101의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 185 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 5 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 101, 186, 13013, 10, 188);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 101, 187, 13013, 10, 177, 27, 168);
   end
end

if EVENT == 188 then
   ShowMap(UID, 1);
end

local Check;

if EVENT == 177 then
  Check = CheckExchange(UID, 5)
   if  Check == true then
   RunExchange(UID, 5);
   SaveEvent(UID, 50);
   else
  Ret = 1;	
  end	 
end

------------------------------------------------------
-- 웜 퇴치 끝 
------------------------------------------------------

------------------------------------------------------
-- 밴디쿳 사냥 시작 
------------------------------------------------------

--시드, 미셀 호출 
if EVENT == 220 then
   SelectMsg(UID, 1, 103, 220, 13013, 24, 221, 14, 222);
end

if EVENT == 221 then
   ShowMap(UID, 4);
   SaveEvent(UID, 63);
end

if EVENT == 222 then
   SaveEvent(UID, 63);
end

if EVENT == 223 then
   SelectMsg(UID, 1, 103, 223, 13013, 24, 221, 14, 222);
end

-- 103번의 속성 0번 4번일 경우 


if EVENT == 225 then
   SelectMsg(UID, 2, 103, 225, 13013,  33, 226);
end

local MonsterSub = 0;

if EVENT == 226 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
			 SelectMsg(UID, 4, 103, 228, 13013, 22, 229, 23, 168);
   else-- 몬스터 서브 퀘스트가 있을때
 			SelectMsg(UID, 2, 103, 173, 13013, 10, 168);
	end
end

if EVENT == 229 then
   SaveEvent(UID, 64);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 231 then
   SaveEvent(UID, 66);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 103, 232, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때 
      SelectMsg(UID, 1, 103, 233, 13013, 14, 168);
   end
end

-- 103번이 1번, 3번일 경우 

local MonsterCount = 0;

if EVENT == 235 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 5 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 103, 237, 13013, 18, 239);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 103, 236, 13013, 10, 238, 27, 168);
   end
end

if EVENT == 239 then
   ShowMap(UID, 7);
end

local Check;

if EVENT == 238 then
  Check = CheckExchange(UID, 7)
   if  Check == true then
   RunExchange(UID, 7);
   SaveEvent(UID, 65);
   else
  Ret = 1;	
  end	 
end

------------------------------------------------------
-- 밴디쿳 사냥 끝
------------------------------------------------------

------------------------------------------------------
-- 케쿤 퇴치 시작 
------------------------------------------------------
--시드, 미셀 호출 
if EVENT == 370 then
--  NATION = CheckNation(UID);
--  if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 320, 3150, 13013, 24, 371);
end

if EVENT == 371 then
   ShowMap(UID, 4);
   SaveEvent(UID, 3322);
end

if EVENT == 372 then
   SelectMsg(UID, 1, 320, 3151, 13013, 24, 371);
end

-- 320번의 속성 0번 4번일 경우 


if EVENT == 300 then
   SelectMsg(UID, 2, 320, 3152, 13013,  3012, 301, 13, 168);
end

local MonsterSub = 0;

if EVENT == 301 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 320, 3153, 13013, 22, 302, 23, 308);
	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 320, 173, 13013, 10, 168);
   end
end

if EVENT == 302 then
   SaveEvent(UID, 3323);
	SelectMsg(UID, 2, 320, 3154, 13013, 10, 168);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 303 then
   SaveEvent(UID, 3325);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 320, 3157, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때 
      SelectMsg(UID, 1, 320, 3158, 13013, 14, 168);
   end
end

-- 320의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 305 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 10 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 320, 3156, 13013, 10, 306);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 320, 3159, 13013, 10, 307, 27, 306);
   end
end

if EVENT == 306 then
   ShowMap(UID, 11);
end

local Check;

if EVENT == 307 then
  Check = CheckExchange(UID, 320)
   if  Check == true then
   RunExchange(UID, 320);
   SaveEvent(UID, 3324);
   else
  Ret = 1;	
  end	 
end

if EVENT == 308 then
	SelectMsg(UID, 2, 320, 3155, 13013, 10, 168);
end
------------------------------------------------------
-- 케군 퇴치 끝 
------------------------------------------------------

------------------------------------------------------
-- 불칸 퇴치 시작 
------------------------------------------------------
--시드, 미셀 호출 
if EVENT == 470 then
--  NATION = CheckNation(UID);
--  if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 321, 170, 13013, 24, 471);
end

if EVENT == 471 then
   ShowMap(UID, 4);
   SaveEvent(UID, 3332);
end

if EVENT == 472 then
   SelectMsg(UID, 1, 321, 172, 13013, 24, 471);
end

-- 321번의 속성 0번 4번일 경우 


if EVENT == 400 then
   SelectMsg(UID, 2, 321, 3160, 13013,  3012, 401, 13, 168);
end

local MonsterSub = 0;

if EVENT == 401 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 321, 3161, 13013, 22, 402, 23, 408);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 321, 173, 13013, 10, 168);
   end
end

if EVENT == 402 then
   SaveEvent(UID, 3333);
	SelectMsg(UID, 2, 320, 3162, 13013, 10, 168);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 403 then
   SaveEvent(UID, 3335);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 321, 3165, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때 
      SelectMsg(UID, 1, 321, 3166, 13013, 14, 168);
   end
end

-- 321의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 405 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 10 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 321, 3164, 13013, 10, 406);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 321, 3167, 13013, 10, 407, 27, 406);
   end
end

if EVENT == 406 then
   ShowMap(UID, 34);
end

local Check;

if EVENT == 407 then
  Check = CheckExchange(UID, 400)
   if  Check == true then
   RunExchange(UID, 321);
   SaveEvent(UID, 3334);
   else
  Ret = 1;	
  end	 
end

if EVENT == 408 then
	SelectMsg(UID, 2, 320, 3163, 13013, 10, 168);
end

------------------------------------------------------
-- 불칸 퇴치 끝 
------------------------------------------------------

------------------------------------------------------
-- 와일드 불칸 퇴치 시작 
------------------------------------------------------
--시드, 미셀 호출 
if EVENT == 570 then
--  NATION = CheckNation(UID);
--  if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 322, 170, 13013, 24, 571);
end

if EVENT == 571 then
   ShowMap(UID, 4);
   SaveEvent(UID, 3342);
end

if EVENT == 572 then
   SelectMsg(UID, 1, 322, 172, 13013, 24, 571);
end

-- 322번의 속성 0번 4번일 경우 


if EVENT == 500 then
   SelectMsg(UID, 2, 322, 3168, 13013,  3012, 501, 13, 168);
end

local MonsterSub = 0;

if EVENT == 501 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 322, 3169, 13013, 22, 502, 23, 508);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 322, 173, 13013, 10, 168);
   end
end

if EVENT == 502 then
	SelectMsg(UID, 2, 320, 3170, 13013, 10, 168);
   SaveEvent(UID, 3343);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 503 then
   SaveEvent(UID, 3345);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 322, 3173, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때 
      SelectMsg(UID, 1, 322, 3174, 13013, 14, 168);
   end
end

-- 322의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 505 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 10 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 322, 3172, 13013, 10, 506);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 322, 3175, 13013, 10, 507, 27, 506);
   end
end

if EVENT == 506 then
   ShowMap(UID, 325);
end

local Check;

if EVENT == 507 then
   Check = CheckExchange(UID, 322)
   if  Check == true then
   RunExchange(UID, 322);
   SaveEvent(UID, 3344);
   else
  Ret = 1;	
  end	 
end

if EVENT == 508 then
	SelectMsg(UID, 2, 320, 3171, 13013, 10, 168);
end

------------------------------------------------------
-- 와일드 불칸 퇴치 끝 
------------------------------------------------------

------------------------------------------------------
-- 케군워리어 퇴치 시작 
------------------------------------------------------
--시드, 미셀 호출 
if EVENT == 670 then
--  NATION = CheckNation(UID);
--  if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 323, 3176, 13013, 3013, 3000);
end

if EVENT == 3000 then
   SelectMsg(UID, 1, 323, 3178, 13013, 3014, 3003);
end

local MonsterSub = 0;

if EVENT == 3003 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
   SelectMsg(UID, 1, 323, 3180, 13013, 22, 671, 23,3005);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 1, 323, 3191, 13013, 10, 168);
   end
end

if EVENT == 671 then-- 시드 수락
   SaveEvent(UID, 3353);
   SelectMsg(UID, 1, 323, 3182, 13013, 10, 168);
end



if EVENT == 672 then
   SelectMsg(UID, 1, 323, 3177, 13013, 24, 3002);
end

if EVENT == 3002 then
   SelectMsg(UID, 1, 323, 3179, 13013, 3014, 3004);
end

local MonsterSub = 0;

if EVENT == 3004 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
   SelectMsg(UID, 1, 323, 3181, 13013, 22, 3007, 23,3006);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 1, 323, 3192, 13013, 10, 168);
   end
end

if EVENT == 3007 then -- 미셀 수락
   SaveEvent(UID, 3353);
   SelectMsg(UID, 1, 323, 3183, 13013, 10, 168);
end

--시드 거절
if EVENT == 3005 then
   SelectMsg(UID, 1, 323, 3184, 13013, 10, 168);
end

if EVENT == 3006 then
   SelectMsg(UID, 1, 323, 3185, 13013, 10, 168);
end


-- 323번의 속성 0번 4번일 경우 


if EVENT == 600 then
   SelectMsg(UID, 2, 323, 3193, 13013,  3017, 601);
end

local MonsterSub = 0;

if EVENT == 601 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 323, 3195, 13013, 3018, 602, 3019, 608);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 323, 3197, 13013, 3001, 168);
   end
end

if EVENT == 602 then
	SelectMsg(UID, 2, 320, 3193, 13013, 10, 168);
   SaveEvent(UID, 3353);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 603 then
   SaveEvent(UID, 3355);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 323, 3187, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때 
      SelectMsg(UID, 1, 323, 3188, 13013, 14, 168);
   end
end

-- 323의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 605 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 10 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 323, 3197, 13013, 10, 168);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 323, 3189, 13013, 10, 607, 27, 168);
   end
end

if EVENT == 606 then
   ShowMap(UID, 326);
end

local Check;

if EVENT == 607 then
   Check = CheckExchange(UID, 323)
   if  Check == true then
   RunExchange(UID, 323);
   SaveEvent(UID, 3354);
   SelectMsg(UID, 2, 323, 3190, 13013, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 608 then
	SelectMsg(UID, 2, 320, 3197, 13013, 10, 168);
end

------------------------------------------------------
-- 케군워리어 퇴치 끝 
------------------------------------------------------

------------------------------------------------------
-- 가볼트 퇴치 시작 
------------------------------------------------------
--시드, 미셀 호출 
if EVENT == 770 then
--  NATION = CheckNation(UID);
--  if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 324, 3798, 13013, 10, 771);
end

if EVENT == 771 then
   ShowMap(UID, 4);
   SaveEvent(UID, 3362);
end

if EVENT == 772 then
   SelectMsg(UID, 1, 324, 3799, 13013, 10, 771);
end

-- 324번의 속성 0번 4번일 경우 


if EVENT == 700 then
   SelectMsg(UID, 2, 324, 3800, 13013,  3012, 701, 13, 168);
end

local MonsterSub = 0;

if EVENT == 701 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 324, 3801, 13013, 22, 702, 23, 708);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 324, 173, 13013, 10, 168);
   end
end

if EVENT == 702 then
	SelectMsg(UID, 2, 320, 3802, 13013, 10, 168);
   SaveEvent(UID, 3363);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 703 then
   SaveEvent(UID, 3365);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 324, 3805, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때 
      SelectMsg(UID, 1, 324, 3806, 13013, 14, 168);
   end
end

-- 324의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 705 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 10 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 324, 3804, 13013, 10, 706);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 324, 3807, 13013, 10, 707, 27, 706);
   end
end

if EVENT == 706 then
   ShowMap(UID, 12);
end

local Check;

if EVENT == 707 then
   Check = CheckExchange(UID, 324)
   if  Check == true then
   RunExchange(UID, 324);
   SaveEvent(UID, 3364);
   else
  Ret = 1;	
  end	 
end

if EVENT == 708 then
	SelectMsg(UID, 2, 320, 3803, 13013, 10, 168);
end

------------------------------------------------------
-- 가볼트 퇴치 끝 
------------------------------------------------------

------------------------------------------------------
-- 벌쳐 퇴치 시작 
------------------------------------------------------
--시드, 미셀 호출 
if EVENT == 870 then
--  NATION = CheckNation(UID);
--  if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 325, 170, 13013, 24, 871);
end

if EVENT == 871 then
   ShowMap(UID, 4);
   SaveEvent(UID, 3372);
end

if EVENT == 872 then
   SelectMsg(UID, 1, 325, 172, 13013, 24, 871);
end

-- 325번의 속성 0번 4번일 경우 


if EVENT == 800 then
   SelectMsg(UID, 2, 325, 3808, 13013,  3012, 801, 13, 168);
end

local MonsterSub = 0;

if EVENT == 801 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 325, 3809, 13013, 22, 802, 23, 808);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 325, 173, 13013, 10, 168);
   end
end

if EVENT == 802 then
	SelectMsg(UID, 2, 320, 3810, 13013, 10, 168);
   SaveEvent(UID, 3373);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 803 then
   SaveEvent(UID, 3375);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 325, 3813, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때 
      SelectMsg(UID, 1, 325, 3814, 13013, 14, 168);
   end
end

-- 325의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 805 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 10 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 325, 3812, 13013, 10, 806);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 325, 3815, 13013, 10, 807, 27, 806);
   end
end

if EVENT == 806 then
end

local Check;

if EVENT == 807 then
   Check = CheckExchange(UID, 325)
   if  Check == true then
   RunExchange(UID, 325);
   SaveEvent(UID, 3374);
   else
  Ret = 1;	
  end	 
end

if EVENT == 808 then
	SelectMsg(UID, 2, 320, 3811, 13013, 10, 168);
end

------------------------------------------------------
-- 벌쳐 퇴치 끝 
------------------------------------------------------

------------------------------------------------------
-- 샤일란 퇴치 시작 
------------------------------------------------------
--시드, 미셀 호출 
if EVENT == 970 then
--  NATION = CheckNation(UID);
--  if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 326, 170, 13013, 24, 971);
end

if EVENT == 971 then
   ShowMap(UID, 4);
   SaveEvent(UID, 3382);
end

if EVENT == 972 then
   SelectMsg(UID, 1, 326, 172, 13013, 24, 971);
end

-- 326번의 속성 0번 4번일 경우 


if EVENT == 900 then
   SelectMsg(UID, 2, 326, 3816, 13013,  3012, 901, 13, 168);
end

local MonsterSub = 0;

if EVENT == 901 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 326, 3817, 13013, 22, 902, 23, 908);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 326, 173, 13013, 10, 168);
   end
end

if EVENT == 902 then
	SelectMsg(UID, 2, 320, 3818, 13013, 10, 168);
   SaveEvent(UID, 3383);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 903 then
    SaveEvent(UID, 3385);
  NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 326, 3821, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때 
      SelectMsg(UID, 1, 326, 3822, 13013, 14, 168);
   end
end

-- 326의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 905 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 10 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 326, 3820, 13013, 10, 906);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 326, 3823, 13013, 10, 907, 27, 906);
   end
end

if EVENT == 906 then
   ShowMap(UID, 60);
end

local Check;

if EVENT == 907 then
   Check = CheckExchange(UID, 326)
   if  Check == true then
   RunExchange(UID, 326);
   SaveEvent(UID, 3384);
   else
  Ret = 1;	
  end	 
end

if EVENT == 908 then
	SelectMsg(UID, 2, 320, 3819, 13013, 10, 168);
end

------------------------------------------------------
-- 샤일란 퇴치 끝 
------------------------------------------------------

------------------------------------------------------
-- 웨어울프 퇴치 시작 
------------------------------------------------------
--시드, 미셀 호출 
if EVENT == 1070 then
--  NATION = CheckNation(UID);
--  if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 327, 3827, 13013, 3005, 1080);
end

if EVENT == 1080 then
      SelectMsg(UID, 1, 327, 3829, 13013, 3013, 1081);
end

if EVENT == 1081 then
      SelectMsg(UID, 1, 327, 3831, 13013, 3014, 1082);
end

if EVENT == 1082 then
      SelectMsg(UID, 1, 327, 3833, 13013, 3003, 1071);
end

if EVENT == 1071 then
   ShowMap(UID, 4);
   SaveEvent(UID, 3392);
end

if EVENT == 1072 then
   SelectMsg(UID, 1, 327, 3828, 13013, 3005, 1083);
end

if EVENT == 1083 then
   SelectMsg(UID, 1, 327, 3830, 13013, 3013, 1084);
end

if EVENT == 1084 then
   SelectMsg(UID, 1, 327, 3832, 13013, 3014, 1085);
end

if EVENT == 1085 then
   SelectMsg(UID, 1, 327, 3834, 13013, 3003, 1071);
end

-- 327번의 속성 0번 4번일 경우 


if EVENT == 1000 then
   SelectMsg(UID, 2, 327, 3835, 13013,  3012, 1001, 13, 168);
end

local MonsterSub = 0;

if EVENT == 1001 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 327, 3836, 13013, 22, 1002, 23, 1008);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 327, 173, 13013, 10, 168);
   end
end

if EVENT == 1002 then
	SelectMsg(UID, 2, 320, 3838, 13013, 10, 168);
   SaveEvent(UID, 3393);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 1003 then
   SaveEvent(UID, 3395);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 327, 3841, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때 
      SelectMsg(UID, 1, 327, 3842, 13013, 14, 168);
   end
end

-- 327의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 1005 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 10 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 327, 3840, 13013, 10, 1006);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 327, 3843, 13013, 10, 1007, 27, 1006);
   end
end

if EVENT == 1006 then
   ShowMap(UID, 35);
end

local Check;

if EVENT == 1007 then
   Check = CheckExchange(UID, 400)
   if  Check == true then
   RunExchange(UID, 327);
   SaveEvent(UID, 3394);
	SelectMsg(UID, 2, 320, 3844, 13013, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 1008 then
	SelectMsg(UID, 2, 320, 3839, 13013, 10, 168);
end

------------------------------------------------------
-- 웨어울프 퇴치 끝 
------------------------------------------------------

--Kekoon Captain hunt
if EVENT == 9205 then
   SelectMsg(UID, 2, 886, 8667, 13013,  3012, 9206, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9206 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 886, 8667, 13013, 22, 9207, 23, 9208);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 886, 173, 13013, 10, 168);
   end
end

if EVENT == 9207 then
	SelectMsg(UID, 2, 886, 8667, 13013, 10, 168);
   SaveEvent(UID, 5275);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9210 then
   SaveEvent(UID, 5277);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 886, 8667, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때 
      SelectMsg(UID, 1, 886, 8667, 13013, 14, 168);
   end
end

-- 886의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 9215 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 10 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 886, 8667, 13013, 10, 9216);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 886, 8667, 13013, 10, 9217, 27, 9216);
   end
end

if EVENT == 9216 then
   ShowMap(UID, 54);
end

local Check;

if EVENT == 9217 then
   Check = CheckExchange(UID, 1080)
   if  Check == true then
   RunExchange(UID, 1080);
   SaveEvent(UID, 5276);
	SelectMsg(UID, 2, 320, 8667, 13013, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9208 then
	SelectMsg(UID, 2, 320, 8667, 13013, 10, 168);
end
--Kekoon Captain hunt
--Giant Bulcan hunt
if EVENT == 9225 then
   SelectMsg(UID, 2, 887, 8668, 13013, 3012, 9226, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9226 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 887, 8668, 13013, 22, 9227, 23, 9228);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 887, 173, 13013, 10, 168);
   end
end

if EVENT == 9227 then
	SelectMsg(UID, 2, 887, 8668, 13013, 10, 168);
	SaveEvent(UID, 5282);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9230 then
   SaveEvent(UID, 5284);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 887, 8668, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때 
      SelectMsg(UID, 1, 887, 8668, 13013, 14, 168);
   end
end

-- 887의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 9235 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 30 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 887, 8668, 13013, 10, 9236);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 887, 8668, 13013, 10, 9237, 27, 9236);
   end
end

if EVENT == 9236 then
   ShowMap(UID, 38);
end

local Check;

if EVENT == 9237 then
   Check = CheckExchange(UID, 1081)
   if  Check == true then
   RunExchange(UID, 1081);
   SaveEvent(UID, 5283);
	SelectMsg(UID, 2, 887, 8668, 13013, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9238 then
	SelectMsg(UID, 2, 887, 8668, 13013, 10, 168);
end
--Giant Bulcan hunt
--Giant Gavolt hunt
if EVENT == 9245 then
   SelectMsg(UID, 2, 888, 8669, 13013, 3012, 9246, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9246 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 888, 8669, 13013, 22, 9247, 23, 9258);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 888, 173, 13013, 10, 168);
   end
end

if EVENT == 9247 then
	SelectMsg(UID, 2, 888, 8669, 13013, 10, 168);
	SaveEvent(UID, 5289);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9250 then
   SaveEvent(UID, 5291);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 888, 8669, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때 
      SelectMsg(UID, 1, 888, 8669, 13013, 14, 168);
   end
end

-- 888의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 9255 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 20 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 888, 8669, 13013, 10, 9256);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 888, 8669, 13013, 10, 9257, 27, 9256);
   end
end

if EVENT == 9256 then
   ShowMap(UID, 12);
end

local Check;

if EVENT == 9257 then
   Check = CheckExchange(UID, 1082)
   if  Check == true then
   RunExchange(UID, 1082);
   SaveEvent(UID, 5290);
	SelectMsg(UID, 2, 888, 8669, 13013, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9258 then
	SelectMsg(UID, 2, 888, 8669, 13013, 10, 168);
end
--Giant Gavolt hunt
--Werewolf Hide

if EVENT == 9431 then
   SelectMsg(UID, 1, 889, 8670, 13013, 28, 9432);
end

if EVENT == 9432 then
   ShowMap(UID, 35);
   SaveEvent(UID, 5295);
end

if EVENT == 9433 then
   SelectMsg(UID, 1, 889, 8670, 13013, 29, 9432);
end

-- 대상인의 딸 889번의 속성 0번 4번일 경우

if EVENT == 9434 then
   SelectMsg(UID, 2, 889, 8670, 13013, 29, 8671);
end

if EVENT == 8671 then
   SelectMsg(UID, 4, 889, 8670, 13013, 22, 8672, 23, 168);
end

if EVENT == 8672 then
   SaveEvent(UID, 5296);
end

local NATION = 0;--국가 체크

if EVENT == 9437 then
   SaveEvent(UID, 5298);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 889, 8670, 13013, 32, 9438);
   else -- 엘모일때
      SelectMsg(UID, 1, 889, 8670, 13013, 4080, 9438);
   end
end

if EVENT == 9438 then
   ShowMap(UID, 35);
end


local ITEM_COUNT=0;
local RUN_EXCHANGE ;--재료 교체 함수 만들어 주세요 

if EVENT == 9439 then
   ITEM_COUNT = HowmuchItem(UID, 379254000);
   if  ITEM_COUNT < 10 then -- 재료 없을때
      SelectMsg(UID, 2, 889, 8670, 13013, 18, 9440);
   elseif ITEM_COUNT  > 2 then-- 재료 있을때
      SelectMsg(UID, 2, 889, 8670, 13013, 4006, 9441, 27, 168);      
 
   end
end

if EVENT == 9440 then
   ShowMap(UID, 35);
end



local Check;

if EVENT == 9441 then
  Check = CheckExchange(UID, 1083)
   if  Check == true then
   RunExchange(UID, 1083);  
   SaveEvent(UID, 5297);
   else
  Ret = 1;	
  end	 
end
--Werewolf Hide
--Glyptodont Captain hunt
if EVENT == 9265 then
   SelectMsg(UID, 2, 890, 8671, 13013,  3012, 9266, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9266 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 890, 8671, 13013, 22, 9267, 23, 9268);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 890, 8671, 13013, 10, 168);
   end
end

if EVENT == 9267 then
	SelectMsg(UID, 2, 890, 8671, 13013, 10, 168);
   SaveEvent(UID, 5303);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9270 then
   SaveEvent(UID, 5305);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 890, 8671, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 890, 8671, 13013, 14, 168);
   end
end

-- 890의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 9275 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 1 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 890, 8671, 13013, 10, 9276);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 890, 8671, 13013, 10, 9277, 27, 9276);
   end
end

if EVENT == 9276 then
   ShowMap(UID, 595);
end

local Check;

if EVENT == 9277 then
   Check = CheckExchange(UID, 1084)
   if  Check == true then
   RunExchange(UID, 1084);
   SaveEvent(UID, 5304);
	SelectMsg(UID, 2, 320, 8671, 13013, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9268 then
	SelectMsg(UID, 2, 320, 8671, 13013, 10, 168);
end
--Glyptodont Captain hunt
--Gloomwing hunt
if EVENT == 9285 then
   SelectMsg(UID, 2, 891, 8672, 13013,  3012, 9286, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9286 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 891, 8672, 13013, 22, 9287, 23, 9288);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 891, 8672, 13013, 10, 168);
   end
end

if EVENT == 9287 then
	SelectMsg(UID, 2, 891, 8672, 13013, 10, 168);
   SaveEvent(UID, 5310);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9290 then
   SaveEvent(UID, 5312);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 891, 8672, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 891, 8672, 13013, 14, 168);
   end
end

-- 891의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 9295 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 30 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 891, 8672, 13013, 10, 9296);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 891, 8672, 13013, 10, 9297, 27, 9296);
   end
end

if EVENT == 9296 then
   ShowMap(UID, 596);
end

local Check;

if EVENT == 9297 then
   Check = CheckExchange(UID, 1085)
   if  Check == true then
   RunExchange(UID, 1085);
   SaveEvent(UID, 5311);
	SelectMsg(UID, 2, 320, 8672, 13013, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9288 then
	SelectMsg(UID, 2, 320, 8672, 13013, 10, 168);
end
--Gloomwing hunt
--Orc Watcher hunt
if EVENT == 9305 then
   SelectMsg(UID, 2, 892, 8673, 13013,  3012, 9306, 13, 168);
end

local MonsterSub = 0;

if EVENT == 9306 then
   MonsterSub = ExistMonsterQuestSub(UID);
   if MonsterSub == 0 then -- 몬스터 서브 퀘스트가 없을때
      SelectMsg(UID, 4, 892, 8673, 13013, 22, 9307, 23, 9308);

	else-- 몬스터 서브 퀘스트가 있을때
 		SelectMsg(UID, 2, 892, 8673, 13013, 10, 168);
   end
end

if EVENT == 9307 then
	SelectMsg(UID, 2, 892, 8673, 13013, 10, 168);
   SaveEvent(UID, 5317);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 9310 then
   SaveEvent(UID, 5319);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 892, 8673, 13013, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 892, 8673, 13013, 14, 168);
   end
end

-- 892의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 9315 then
   MonsterCount  = CountMonsterQuestSub(UID, 32001);
   if  MonsterCount < 20 then -- 사냥이 남았을때
      SelectMsg(UID, 2, 892, 8673, 13013, 10, 9316);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 892, 8673, 13013, 10, 9317, 27, 9316);
   end
end

if EVENT == 9316 then
   ShowMap(UID, 597);
end

local Check;

if EVENT == 9317 then
   Check = CheckExchange(UID, 1086)
   if  Check == true then
   RunExchange(UID, 1086);
   SaveEvent(UID, 5318);
	SelectMsg(UID, 2, 320, 8673, 13013, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 9308 then
	SelectMsg(UID, 2, 320, 8673, 13013, 10, 168);
end
--Orc Watcher hunt

return Ret;
