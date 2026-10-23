local UserClass;
local QuestNum;
local Ret = 0;
local NPC = 24435;

if EVENT == 165 then
	QuestNum = SearchQuest(UID, NPC);
		if QuestNum == 0 then --해당 NPC에게 할수 있는 퀘스트가 0개 일때 
			 SelectMsg(UID, 2, -1, 4938, NPC, 10, 193);
			 Ret = 1; 
		elseif QuestNum > 1 and  QuestNum < 100 then--해당 NPC에게 할수 있는 퀘스트가 1개 이상 일때 
          NpcMsg(UID, 4938, NPC)
      else --해당 NPC에게 할수 있는 퀘스트가 1개 일때 
          EVENT = QuestNum
		end
end

if EVENT == 193 then
    Ret = 1;
end
--Silver Feather 
if EVENT == 252 then
   SelectMsg(UID, 2, 565, 6115, 24435,  3012, 253, 13, 168);
end

local MonsterSub = 0;

if EVENT == 253 then
      SelectMsg(UID, 4, 565, 6115, 24435, 22, 254, 23, 255);
end

if EVENT == 254 then
	SelectMsg(UID, 2, 565, 6115, 24435, 10, 168);
   SaveEvent(UID, 9861);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 255 then
   SaveEvent(UID, 9863);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 565, 6115, 24435, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 565, 6115, 24435, 14, 168);
   end
end

-- 565의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 256 then
 ITEM_COUNT = HowmuchItem(UID, 379272000);  
   if  ITEM_COUNT < 5 then -- 재료 없을때
      SelectMsg(UID, 2, 565, 6115, 24435, 10, 257);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 565, 6115, 24435, 10, 258, 27, 257);
   end
end

if EVENT == 257 then
   ShowMap(UID, 532);
end

local Check;

if EVENT == 258 then
   Check = CheckExchange(UID, 543)
   if  Check == true then
   RunExchange(UID, 543);
   SaveEvent(UID, 9862);
	SelectMsg(UID, 2, 320, 6115, 24435, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 255 then
	SelectMsg(UID, 2, 320, 6115, 24435, 10, 168);
end
--Silver Feather 
--Skeleton warrior 
if EVENT == 262 then
   SelectMsg(UID, 2, 566, 6127, 24435,  3012, 263, 13, 168);
end

local MonsterSub = 0;

if EVENT == 263 then
      SelectMsg(UID, 4, 566, 6127, 24435, 22, 264, 23, 265);
end

if EVENT == 264 then
	SelectMsg(UID, 2, 566, 6127, 24435, 10, 168);
   SaveEvent(UID, 9873);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 265 then
   SaveEvent(UID, 9875);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 566, 6127, 24435, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 566, 6127, 24435, 14, 168);
   end
end

-- 566의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 266 then
 ITEM_COUNT = HowmuchItem(UID, 379253000);  
   if  ITEM_COUNT < 7 then -- 재료 없을때
      SelectMsg(UID, 2, 566, 6127, 24435, 10, 267);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 566, 6127, 24435, 10, 268, 27, 267);
   end
end

if EVENT == 267 then
   ShowMap(UID, 575);
end

local Check;

if EVENT == 268 then
   Check = CheckExchange(UID, 544)
   if  Check == true then
   RunExchange(UID, 544);
   SaveEvent(UID, 9874);
	SelectMsg(UID, 2, 320, 6127, 24435, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 265 then
	SelectMsg(UID, 2, 320, 6127, 24435, 10, 168);
end
--Skeleton warrior 
--Skeleton knight 
if EVENT == 272 then
   SelectMsg(UID, 2, 567, 6139, 24435,  3012, 273, 13, 168);
end

local MonsterSub = 0;

if EVENT == 273 then
      SelectMsg(UID, 4, 567, 6139, 24435, 22, 274, 23, 275);
end

if EVENT == 274 then
	SelectMsg(UID, 2, 567, 6139, 24435, 10, 168);
   SaveEvent(UID, 9885);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 275 then
   SaveEvent(UID, 9887);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 567, 6139, 24435, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 567, 6139, 24435, 14, 168);
   end
end

-- 567의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 276 then
 ITEM_COUNT = HowmuchItem(UID, 379275000);  
   if  ITEM_COUNT < 7 then -- 재료 없을때
      SelectMsg(UID, 2, 567, 6139, 24435, 10, 277);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 567, 6139, 24435, 10, 278, 27, 277);
   end
end

if EVENT == 277 then
   ShowMap(UID, 534);
end

local Check;

if EVENT == 278 then
   Check = CheckExchange(UID, 545)
   if  Check == true then
   RunExchange(UID, 545);
   SaveEvent(UID, 9886);
	SelectMsg(UID, 2, 320, 6139, 24435, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 275 then
	SelectMsg(UID, 2, 320, 6139, 24435, 10, 168);
end
--Skeleton knight 
--Death knight 
if EVENT == 282 then
   SelectMsg(UID, 2, 568, 6151, 24435,  3012, 283, 13, 168);
end

local MonsterSub = 0;

if EVENT == 283 then
      SelectMsg(UID, 4, 568, 6151, 24435, 22, 284, 23, 285);
end

if EVENT == 284 then
	SelectMsg(UID, 2, 568, 6151, 24435, 10, 168);
   SaveEvent(UID, 9897);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 285 then
   SaveEvent(UID, 9899);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 568, 6151, 24435, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 568, 6151, 24435, 14, 168);
   end
end

-- 568의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 286 then
 ITEM_COUNT = HowmuchItem(UID, 379275000);  
 ITEM_COUNT1 = HowmuchItem(UID, 379253000); 
   if  ITEM_COUNT < 5 and ITEM_COUNT1 < 5 then -- 재료 없을때
      SelectMsg(UID, 2, 568, 6151, 24435, 10, 287);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 568, 6151, 24435, 10, 288, 27, 287);
   end
end

if EVENT == 287 then
   ShowMap(UID, 538);
end

local Check;

if EVENT == 288 then
   Check = CheckExchange(UID, 546)
   if  Check == true then
   RunExchange(UID, 546);
   SaveEvent(UID, 9898);
	SelectMsg(UID, 2, 320, 6151, 24435, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 285 then
	SelectMsg(UID, 2, 320, 6151, 24435, 10, 168);
end
--Death knight 
--Lard orc 
if EVENT == 292 then
   SelectMsg(UID, 2, 569, 6163, 24435,  3012, 293, 13, 168);
end

local MonsterSub = 0;

if EVENT == 293 then
      SelectMsg(UID, 4, 569, 6163, 24435, 22, 294, 23, 295);
end

if EVENT == 294 then
	SelectMsg(UID, 2, 569, 6163, 24435, 10, 168);
   SaveEvent(UID, 9909);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 295 then
   SaveEvent(UID, 9911);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 569, 6163, 24435, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 569, 6163, 24435, 14, 168);
   end
end

-- 569의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 296 then
 ITEM_COUNT = HowmuchItem(UID, 379277000);  
   if  ITEM_COUNT < 7 then -- 재료 없을때
      SelectMsg(UID, 2, 569, 6163, 24435, 10, 297);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 569, 6163, 24435, 10, 298, 27, 297);
   end
end

if EVENT == 297 then
   ShowMap(UID, 579);
end

local Check;

if EVENT == 298 then
   Check = CheckExchange(UID, 547)
   if  Check == true then
   RunExchange(UID, 547);
   SaveEvent(UID, 9910);
	SelectMsg(UID, 2, 320, 6163, 24435, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 295 then
	SelectMsg(UID, 2, 320, 6163, 24435, 10, 168);
end
--Lard orc 
--Vatalion 
if EVENT == 302 then
   SelectMsg(UID, 2, 570, 6175, 24435,  3012, 303, 13, 168);
end

local MonsterSub = 0;

if EVENT == 303 then
      SelectMsg(UID, 4, 570, 6175, 24435, 22, 304, 23, 305);
end

if EVENT == 304 then
	SelectMsg(UID, 2, 570, 6175, 24435, 10, 168);
   SaveEvent(UID, 9921);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 305 then
   SaveEvent(UID, 9923);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 570, 6175, 24435, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 570, 6175, 24435, 14, 168);
   end
end

-- 570의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 306 then
 ITEM_COUNT = HowmuchItem(UID, 379276000);  
   if  ITEM_COUNT < 7 then -- 재료 없을때
      SelectMsg(UID, 2, 570, 6175, 24435, 10, 307);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 570, 6175, 24435, 10, 308, 27, 307);
   end
end

if EVENT == 307 then
   ShowMap(UID, 581);
end

local Check;

if EVENT == 308 then
   Check = CheckExchange(UID, 548)
   if  Check == true then
   RunExchange(UID, 548);
   SaveEvent(UID, 9922);
	SelectMsg(UID, 2, 320, 6175, 24435, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 305 then
	SelectMsg(UID, 2, 320, 6175, 24435, 10, 168);
end
--Vatalion 
--Meganthereon 
if EVENT == 312 then
   SelectMsg(UID, 2, 571, 6187, 24435,  3012, 313, 13, 168);
end

local MonsterSub = 0;

if EVENT == 313 then
      SelectMsg(UID, 4, 571, 6187, 24435, 22, 314, 23, 315);
end

if EVENT == 314 then
	SelectMsg(UID, 2, 571, 6187, 24435, 10, 168);
   SaveEvent(UID, 9933);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 315 then
   SaveEvent(UID, 9935);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 571, 6187, 24435, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 571, 6187, 24435, 14, 168);
   end
end

-- 571의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 316 then
 ITEM_COUNT = HowmuchItem(UID, 379272000);  
   if  ITEM_COUNT < 10 then -- 재료 없을때
      SelectMsg(UID, 2, 571, 6187, 24435, 10, 317);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 571, 6187, 24435, 10, 318, 27, 317);
   end
end

if EVENT == 317 then
   ShowMap(UID, 583);
end

local Check;

if EVENT == 318 then
   Check = CheckExchange(UID, 549)
   if  Check == true then
   RunExchange(UID, 549);
   SaveEvent(UID, 9934);
	SelectMsg(UID, 2, 320, 6187, 24435, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 315 then
	SelectMsg(UID, 2, 320, 6187, 24435, 10, 168);
end
--Meganthereon 
--Paralyzer 
if EVENT == 102 then
   SelectMsg(UID, 2, 559, 4943, 24435,  3012, 103, 13, 168);
end

local MonsterSub = 0;

if EVENT == 103 then
      SelectMsg(UID, 4, 559, 4943, 24435, 22, 104, 23, 105);
end

if EVENT == 104 then
	SelectMsg(UID, 2, 559, 4943, 24435, 10, 168);
   SaveEvent(UID, 9959);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 105 then
   SaveEvent(UID, 9961);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 559, 4943, 24435, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 559, 4943, 24435, 14, 168);
   end
end

-- 559의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 106 then
 ITEM_COUNT = HowmuchItem(UID, 392011000);  
   if  ITEM_COUNT < 5 then -- 재료 없을때
      SelectMsg(UID, 2, 559, 4943, 24435, 10, 107);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 559, 4943, 24435, 10, 108, 27, 107);
   end
end

if EVENT == 107 then
   ShowMap(UID, 571);
end

local Check;

if EVENT == 108 then
   Check = CheckExchange(UID, 537)
   if  Check == true then
   RunExchange(UID, 537);
   SaveEvent(UID, 9960);
	SelectMsg(UID, 2, 320, 4943, 24435, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 105 then
	SelectMsg(UID, 2, 320, 4943, 24435, 10, 168);
end
--Paralyzer 
--Dire wolf 
if EVENT == 202 then
   SelectMsg(UID, 2, 560, 4955, 24435,  3012, 203, 13, 168);
end

local MonsterSub = 0;

if EVENT == 203 then
      SelectMsg(UID, 4, 560, 4955, 24435, 22, 204, 23, 205);
end

if EVENT == 204 then
	SelectMsg(UID, 2, 560, 4955, 24435, 10, 168);
   SaveEvent(UID, 9971);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 205 then
   SaveEvent(UID, 9973);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 560, 4955, 24435, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 560, 4955, 24435, 14, 168);
   end
end

-- 560의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 206 then
 ITEM_COUNT = HowmuchItem(UID, 379254000);  
 ITEM_COUNT1 = HowmuchItem(UID, 910165000); 
   if  ITEM_COUNT < 5 and ITEM_COUNT1 < 5 then -- 재료 없을때
      SelectMsg(UID, 2, 560, 4955, 24435, 10, 207);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 560, 4955, 24435, 10, 208, 27, 207);
   end
end

if EVENT == 207 then
   ShowMap(UID, 524);
end

local Check;

if EVENT == 208 then
   Check = CheckExchange(UID, 538)
   if  Check == true then
   RunExchange(UID, 538);
   SaveEvent(UID, 9972);
	SelectMsg(UID, 2, 320, 4955, 24435, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 205 then
	SelectMsg(UID, 2, 320, 4955, 24435, 10, 168);
end
--Dire wolf 
--Smirdon 
if EVENT == 212 then
   SelectMsg(UID, 2, 561, 4967, 24435,  3012, 213, 13, 168);
end

local MonsterSub = 0;

if EVENT == 213 then
      SelectMsg(UID, 4, 561, 4967, 24435, 22, 214, 23, 215);
end

if EVENT == 214 then
	SelectMsg(UID, 2, 561, 4967, 24435, 10, 168);
   SaveEvent(UID, 9983);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 215 then
   SaveEvent(UID, 9985);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 561, 4967, 24435, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 561, 4967, 24435, 14, 168);
   end
end

-- 561의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 216 then
 ITEM_COUNT = HowmuchItem(UID, 379270000);  
   if  ITEM_COUNT < 5 then -- 재료 없을때
      SelectMsg(UID, 2, 561, 4967, 24435, 10, 217);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 561, 4967, 24435, 10, 218, 27, 217);
   end
end

if EVENT == 217 then
   ShowMap(UID, 526);
end

local Check;

if EVENT == 218 then
   Check = CheckExchange(UID, 539)
   if  Check == true then
   RunExchange(UID, 539);
   SaveEvent(UID, 9984);
	SelectMsg(UID, 2, 320, 4967, 24435, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 215 then
	SelectMsg(UID, 2, 320, 4967, 24435, 10, 168);
end
--Smirdon 
--Smirdon 
if EVENT == 222 then
   SelectMsg(UID, 2, 562, 4979, 24435,  3012, 223, 13, 168);
end

local MonsterSub = 0;

if EVENT == 223 then
      SelectMsg(UID, 4, 562, 4979, 24435, 22, 224, 23, 225);
end

if EVENT == 224 then
	SelectMsg(UID, 2, 562, 4979, 24435, 10, 168);
   SaveEvent(UID, 9995);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 225 then
   SaveEvent(UID, 9997);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 562, 4979, 24435, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 562, 4979, 24435, 14, 168);
   end
end

-- 562의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 226 then
 ITEM_COUNT = HowmuchItem(UID, 379271000);  
   if  ITEM_COUNT < 5 then -- 재료 없을때
      SelectMsg(UID, 2, 562, 4979, 24435, 10, 227);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 562, 4979, 24435, 10, 228, 27, 227);
   end
end

if EVENT == 227 then
   ShowMap(UID, 525);
end

local Check;

if EVENT == 228 then
   Check = CheckExchange(UID, 540)
   if  Check == true then
   RunExchange(UID, 540);
   SaveEvent(UID, 9996);
	SelectMsg(UID, 2, 320, 4979, 24435, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 225 then
	SelectMsg(UID, 2, 320, 4979, 24435, 10, 168);
end
--Smirdon 
--Shadow seeker 
if EVENT == 232 then
   SelectMsg(UID, 2, 563, 4991, 24435,  3012, 233, 13, 168);
end

local MonsterSub = 0;

if EVENT == 233 then
      SelectMsg(UID, 4, 563, 4991, 24435, 22, 234, 23, 235);
end

if EVENT == 234 then
	SelectMsg(UID, 2, 563, 4991, 24435, 10, 168);
   SaveEvent(UID, 10007);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 235 then
   SaveEvent(UID, 10009);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 563, 4991, 24435, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 563, 4991, 24435, 14, 168);
   end
end

-- 563의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 236 then
 ITEM_COUNT = HowmuchItem(UID, 379273000);  
   if  ITEM_COUNT < 5 then -- 재료 없을때
      SelectMsg(UID, 2, 563, 4991, 24435, 10, 237);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 563, 4991, 24435, 10, 238, 27, 237);
   end
end

if EVENT == 237 then
   ShowMap(UID, 530);
end

local Check;

if EVENT == 238 then
   Check = CheckExchange(UID, 541)
   if  Check == true then
   RunExchange(UID, 541);
   SaveEvent(UID, 10008);
	SelectMsg(UID, 2, 320, 4991, 24435, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 235 then
	SelectMsg(UID, 2, 320, 4991, 24435, 10, 168);
end
--Shadow seeker 
--Rotten eyes 
if EVENT == 242 then
   SelectMsg(UID, 2, 564, 6103, 24435,  3012, 243, 13, 168);
end

local MonsterSub = 0;

if EVENT == 243 then
      SelectMsg(UID, 4, 564, 6103, 24435, 22, 244, 23, 245);
end

if EVENT == 244 then
	SelectMsg(UID, 2, 564, 6103, 24435, 10, 168);
   SaveEvent(UID, 10019);
end

-- 재료를 다 모았을때 시드와 미셀의 또 등장 
if EVENT == 245 then
   SaveEvent(UID, 10021);
   NATION = CheckNation(UID);
   if NATION == 1 then -- 카루스 일때
      SelectMsg(UID, 1, 564, 6103, 24435, 14, 168);
   elseif NATION == 2 then -- 엘모일때
      SelectMsg(UID, 1, 564, 6103, 24435, 14, 168);
   end
end

-- 564의 1번 3번인 경우 

local MonsterCount = 0;

if EVENT == 246 then
 ITEM_COUNT = HowmuchItem(UID, 379274000);  
   if  ITEM_COUNT < 5 then -- 재료 없을때
      SelectMsg(UID, 2, 564, 6103, 24435, 10, 247);
   else-- 사냥이 완료 되었을때 
      SelectMsg(UID, 4, 564, 6103, 24435, 10, 248, 27, 247);
   end
end

if EVENT == 247 then
   ShowMap(UID, 577);
end

local Check;

if EVENT == 248 then
   Check = CheckExchange(UID, 542)
   if  Check == true then
   RunExchange(UID, 542);
   SaveEvent(UID, 10020);
	SelectMsg(UID, 2, 320, 6103, 24435, 10, 168);
   else
  Ret = 1;	
  end	 
end

if EVENT == 245 then
	SelectMsg(UID, 2, 320, 6103, 24435, 10, 168);
end
--Rotten eyes 
