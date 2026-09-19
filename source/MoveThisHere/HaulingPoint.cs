using KSerialization;
using System;
using System.Collections.Generic;
using UnityEngine;
using STRINGS;
using Newtonsoft.Json.Linq;
using TUNING;

namespace MoveThisHere
{
    public class HaulingPoint : KMonoBehaviour, ISim1000ms, ISingleSliderControl //, IUserControlledCapacity
    {
#pragma warning disable CS0649
#pragma warning disable IDE0044
        [MyCmpGet]
        private Storage storage;
#pragma warning restore IDE0044
#pragma warning restore CS0649

        [Serialize]
        public bool allowManualPumpingStationFetching;

        [Serialize]
        private float userMaxCapacity = float.PositiveInfinity;

        [Serialize]
        private bool willSelfDestruct = false;

        [Serialize]
        public bool willSpill = false;

        private Tag[] forbidden_tags;

        public float totalMaxCapacity;
        //create new float for total max capacity and now using public capacitykg in Storage to hold user capacity which used to be the max
        //this is a clumsy workaround to use a custom slider to hold user capacity, rather than default iusercontrolledcapacity which is null
        //all because I can't get IUserControlledCapacity to allow decimal values, and I know you nerds are gonna wanna store 35g or something

        public string SliderTitleKey => STRINGS.BUILDINGS.PREFABS.HAULINGPOINT.SLIDER_TITLE;

        public string SliderUnits => GameUtil.GetCurrentMassUnit();
        public float GetSliderMax(int index)
        {
            return totalMaxCapacity;
        }

        public float GetSliderMin(int index)
        {
            return 0.0f;
        }

        public float GetSliderValue(int index)
        {
            return userMaxCapacity;
        }

        public string GetSliderTooltip(int index) => STRINGS.BUILDINGS.PREFABS.HAULINGPOINT.SLIDER_TOOLTIP;

        public string GetSliderTooltipKey(int index)
        {
            return "";
        }
        public void SetSliderValue(float value, int index)
        {
            if (value != userMaxCapacity) //setslidervalue runs each time slider appears AND if changed - check if actually changed to avoid unncessary job interruptions
            {
                if (value > 100f)
                {
                    value = (float)Math.Round((decimal)value);
                    //will round off decimals above 100kg to avoid weird 5g bits when slider is moved instead of typed number
                    //if you really want 200.15kg, use two hauling points
                }
                storage.capacityKg = value;
                userMaxCapacity = value; //set both local and Storage variable, local variable gets kept on save/load
                filteredStorage.FilterChanged();
            }
        }
        public int SliderDecimalPlaces(int index)
        {
            return 3; //UI limitations make less than 1g a pain to implement
        }


        public float AmountStored => storage.MassStored();


        protected override void OnPrefabInit()
        {
            Initialize(use_logic_meter: false);
        }


        protected FilteredStorageHaulingPoint filteredStorage;

        public string choreTypeID = Db.Get().ChoreTypes.StorageFetch.Id;

        private static readonly EventSystem.IntraObjectHandler<HaulingPoint> OnCopySettingsDelegate = new EventSystem.IntraObjectHandler<HaulingPoint>(delegate (HaulingPoint component, object data)
        {
            component.OnCopySettings(data);
        });
        private static readonly EventSystem.IntraObjectHandler<HaulingPoint> OnRefreshUserMenuDelegate = new EventSystem.IntraObjectHandler<HaulingPoint>(delegate (HaulingPoint component, object data)
        {
            component.OnRefreshUserMenu(data);
        });



        protected void Initialize(bool use_logic_meter)
        {
            //initialize comes first, then spawn
            base.OnPrefabInit();

            ChoreType fetch_chore_type = Db.Get().ChoreTypes.Get(choreTypeID);

            forbidden_tags = (allowManualPumpingStationFetching ? new Tag[0] : new Tag[2] { GameTags.LiquidSource, GameTags.GasSource });

            filteredStorage = new FilteredStorageHaulingPoint(this, forbidden_tags, null, use_logic_meter, fetch_chore_type);
            //replacing capacity_control slider in filteredstorage - leave it null and do the logic for it here
            //forbidden tags contains either nothing or pump stations. have to make own copy of filteredstorage just to keep this private field updated

            Subscribe(-905833192, OnCopySettingsDelegate);
            Subscribe(493375141, OnRefreshUserMenuDelegate);

        }

        protected override void OnSpawn()
        {
            base.OnSpawn();

            if (userMaxCapacity >= totalMaxCapacity)
            {
                userMaxCapacity = totalMaxCapacity;
            }
            storage.capacityKg = userMaxCapacity; //set this up since capacitykg isn't serialized, I'm sure there is an easier way but whatever
                                                  //must read serialized variables during onspawn, not initialize, I guess they are not unserialized until now.

            forbidden_tags = (allowManualPumpingStationFetching ? new Tag[0] : new Tag[2] { GameTags.LiquidSource, GameTags.GasSource });
            filteredStorage.SetForbiddenTags(forbidden_tags);
            filteredStorage.FilterChanged();

        }
        private void OnChangeAllowManualPumpingStationFetching()
        {
            allowManualPumpingStationFetching = !allowManualPumpingStationFetching;

            forbidden_tags = (allowManualPumpingStationFetching ? new Tag[0] : new Tag[2] { GameTags.LiquidSource, GameTags.GasSource });
            filteredStorage.SetForbiddenTags(forbidden_tags);
            filteredStorage.FilterChanged();
        }
        private void OnChangeWillSpill()
        {
            willSpill = !willSpill;

            // 倾倒开关变化会影响已排队的手动拆除任务是否仍需要小人
            // Toggling willSpill affects whether a queued manual deconstruct still needs a duplicant.
            GetComponent<DeconstructableHaulingPoint>()?.ReevaluateQueuedDeconstruction();
        }
        private void ToggleWillSelfDestruct()
        {
            willSelfDestruct = !willSelfDestruct;
        }

        protected override void OnCleanUp()
        {
            filteredStorage.CleanUp();
        }


        private void OnCopySettings(object data)
        {
            GameObject gameObject = (GameObject)data;
            if (!(gameObject == null))
            {
                HaulingPoint component = gameObject.GetComponent<HaulingPoint>();
                if (!(component == null))
                {
                    //this is copying settings TO the local variables from clipboard component
                    userMaxCapacity = component.userMaxCapacity;
                    storage.capacityKg = userMaxCapacity;
                    willSelfDestruct = component.willSelfDestruct;
                    willSpill = component.willSpill;
                    allowManualPumpingStationFetching = component.allowManualPumpingStationFetching;
                    forbidden_tags = (allowManualPumpingStationFetching ? new Tag[0] : new Tag[1] { GameTags.LiquidSource });
                    filteredStorage.SetForbiddenTags(forbidden_tags);
                    filteredStorage.FilterChanged();

                }
            }
		}
		public static JObject Blueprints_GetData(GameObject source)
		{
			if (source.TryGetComponent<HaulingPoint>(out var behavior))
			{
				return new JObject()
				{
					{ "userMaxCapacity", behavior.userMaxCapacity},
					{ "willSelfDestruct", behavior.willSelfDestruct},
					{ "willSpill", behavior.willSpill},
					{ "allowManualPumpingStationFetching", behavior.allowManualPumpingStationFetching},
				};
			}
			return null;
		}
		public static void Blueprints_SetData(GameObject target, JObject data)
		{
			if (target.TryGetComponent<HaulingPoint>(out var targetHaulingPoint))
			{
				var token_userMaxCapacity = data.GetValue("userMaxCapacity");
				var token_allowManualPumpingStationFetching = data.GetValue("allowManualPumpingStationFetching");
				var token_willSelfDestruct = data.GetValue("willSelfDestruct");
				var token_willSpill = data.GetValue("willSpill");
				if (token_userMaxCapacity == null || token_willSelfDestruct == null || token_willSpill == null || token_allowManualPumpingStationFetching == null)
					return;
                float userMaxCapacity = token_userMaxCapacity.Value<float>();
                bool willSelfDestruct = token_willSelfDestruct.Value<bool>();;
                bool willSpill = token_willSpill.Value<bool>();
                bool allowManualPumpingStationFetching = token_allowManualPumpingStationFetching.Value<bool>();
				
                targetHaulingPoint.userMaxCapacity = userMaxCapacity;
				targetHaulingPoint.storage.capacityKg = userMaxCapacity;
				targetHaulingPoint.willSelfDestruct = willSelfDestruct;
				targetHaulingPoint.willSpill = willSpill;
				targetHaulingPoint.allowManualPumpingStationFetching = allowManualPumpingStationFetching;
                Tag[] forbidden_tags = (allowManualPumpingStationFetching ? new Tag[0] : new Tag[1] { GameTags.LiquidSource });   
				targetHaulingPoint.forbidden_tags = forbidden_tags;
				targetHaulingPoint.filteredStorage.SetForbiddenTags(forbidden_tags);
				targetHaulingPoint.filteredStorage.FilterChanged();
			}
		}

		private void OnRefreshUserMenu(object data)
        {
            //KIconButtonMenu.ButtonInfo button2 = (allowManualPumpingStationFetching ?
            //    new KIconButtonMenu.ButtonInfo("action_bottler_delivery", UI.USERMENUACTIONS.MANUAL_PUMP_DELIVERY.DENIED.NAME, OnChangeAllowManualPumpingStationFetching, Action.NumActions, null, null, null, UI.USERMENUACTIONS.MANUAL_PUMP_DELIVERY.DENIED.TOOLTIP) :
            //    new KIconButtonMenu.ButtonInfo("action_bottler_delivery", UI.USERMENUACTIONS.MANUAL_PUMP_DELIVERY.ALLOWED.NAME, OnChangeAllowManualPumpingStationFetching, Action.NumActions, null, null, null, UI.USERMENUACTIONS.MANUAL_PUMP_DELIVERY.ALLOWED.TOOLTIP));
            KIconButtonMenu.ButtonInfo autoBottleButton = (allowManualPumpingStationFetching ?
                new KIconButtonMenu.ButtonInfo("action_bottler_delivery", STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.AUTO_BOTTLE_OFF, OnChangeAllowManualPumpingStationFetching, Action.NumActions, null, null, null, STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.AUTO_BOTTLE_OFF_TOOLTIP) :
                new KIconButtonMenu.ButtonInfo("action_bottler_delivery", STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.AUTO_BOTTLE_ON, OnChangeAllowManualPumpingStationFetching, Action.NumActions, null, null, null, STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.AUTO_BOTTLE_ON_TOOLTIP));
            Game.Instance.userMenu.AddButton(base.gameObject, autoBottleButton, 0.4f);

            KIconButtonMenu.ButtonInfo autoDropButton = (willSelfDestruct ?
                new KIconButtonMenu.ButtonInfo("action_empty_contents", STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.AUTO_DROP_OFF, ToggleWillSelfDestruct, Action.NumActions, null, null, null, STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.AUTO_DROP_OFF_TOOLTIP) :
                new KIconButtonMenu.ButtonInfo("action_empty_contents", STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.AUTO_DROP_ON, ToggleWillSelfDestruct, Action.NumActions, null, null, null, STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.AUTO_DROP_ON_TOOLTIP));
            Game.Instance.userMenu.AddButton(base.gameObject, autoDropButton);

            KIconButtonMenu.ButtonInfo autoSpillButton = (willSpill ?
                new KIconButtonMenu.ButtonInfo("action_bottler_delivery", STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.AUTO_SPILL_OFF, OnChangeWillSpill, Action.NumActions, null, null, null, STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.AUTO_SPILL_OFF_TOOLTIP) :
                new KIconButtonMenu.ButtonInfo("action_bottler_delivery", STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.AUTO_SPILL_ON, OnChangeWillSpill, Action.NumActions, null, null, null, STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.AUTO_SPILL_ON_TOOLTIP));
            Game.Instance.userMenu.AddButton(base.gameObject, autoSpillButton);
        }

        public void Sim1000ms(float dt)
        {
            if (!willSelfDestruct) return;
            if (userMaxCapacity <= 0f) return;                                  // 防止除零
            if ((AmountStored / userMaxCapacity) < .99f) return;                //give a little wiggle for sublimination, stock margin doesn't work with low mass

            // 自动拆除走瞬间路径，小人刚送完货就在附近，不需要再排队操作
            // Auto-deconstruct uses the instant path: the duplicant just finished delivering and is nearby, no need to queue a chore.
            GetComponentInParent<DeconstructableHaulingPoint>()?.InstantDeconstruct();
        }

    }

    public class DeconstructableHaulingPoint : Workable
    {

        //modified deconstructable to replace default behavior, this one will deconstruct instantly when given decon order
        //however it won't drop any resources from the building itself, important because it's made of vacuum and this gives an error
        //also drops gas resource in canister form

        // 手动拆除时创建的小人任务；自动拆除不经过它
        // Chore created for manual deconstruction; auto-deconstruct does not go through it.
        private Chore deconstructChore;

        // 累计已工作的时间，用于在 OnWorkTick 里判断完成
        // Accumulated work time, used to determine completion in OnWorkTick.
        private float workElapsedTime;

        // 是否已排队手动拆除（用于按钮显示 拆除 / 取消拆除，及防止重复排队）
        // Whether manual deconstruction is queued (for the Remove / Cancel Remove button, and to prevent double queueing).
        private bool isMarkedForDeconstruction;

        private static readonly EventSystem.IntraObjectHandler<DeconstructableHaulingPoint> OnRefreshUserMenuDelegate = new EventSystem.IntraObjectHandler<DeconstructableHaulingPoint>(delegate (DeconstructableHaulingPoint component, object data)
        {
            component.OnRefreshUserMenu(data);
        });
        private static readonly EventSystem.IntraObjectHandler<DeconstructableHaulingPoint> OnDeconstructDelegate = new EventSystem.IntraObjectHandler<DeconstructableHaulingPoint>(delegate (DeconstructableHaulingPoint component, object data)
        {
            // 原版拆除事件（-790448070）走 RequestDeconstruct，由它按 willSpill 决定是否排队小人
            // The vanilla deconstruct event (-790448070) goes through RequestDeconstruct, which decides based on willSpill whether to queue a duplicant chore.
            component.RequestDeconstruct();
        });
        private CellOffset[] placementOffsets
        {
            get
            {
                Building component = GetComponent<Building>();
                if (component != null)
                {
                    return component.Def.PlacementOffsets;
                }

                Debug.Assert(condition: false, "There's some error with MoveThisHere mod that the developer doesn't understand", this);
                return null;

            }
        }

        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();

            // 复制原版 Deconstructable 的配置，让小人播放正确的拆除动画和状态
            // Copy the vanilla Deconstructable configuration so the duplicant plays the correct deconstruct animation and status.
            this.faceTargetWhenWorking = true;
            this.synchronizeAnims = false;
            this.workerStatusItem = Db.Get().DuplicantStatusItems.Deconstructing;
            this.attributeConverter = Db.Get().AttributeConverters.ConstructionSpeed;
            // this.attributeExperienceMultiplier = DUPLICANTSTATS.ATTRIBUTE_LEVELING.MOST_DAY_EXPERIENCE;
            this.attributeExperienceMultiplier = 0;
            this.minimumAttributeMultiplier = 0.75f;
            this.skillExperienceSkillGroup = Db.Get().SkillGroups.Building.Id;
            this.skillExperienceMultiplier = 0;
            // this.attributeExperienceMultiplier = DUPLICANTSTATS.ATTRIBUTE_LEVELING.MOST_DAY_EXPERIENCE;
            this.multitoolContext = "build";
            this.multitoolHitEffectTag = EffectConfigs.BuildSplashId;
            this.workingPstComplete = null;
            this.workingPstFailed = null;

            // 对齐原版 Dumpable 的倒空时长
            // Match vanilla Dumpable's empty duration.
            this.SetWorkTime(1f);

            Subscribe(493375141, OnRefreshUserMenuDelegate);
            Subscribe(-111137758, OnRefreshUserMenuDelegate);
            Subscribe(-790448070, OnDeconstructDelegate);

            CellOffset[][] table = OffsetGroups.InvertedStandardTable;
            CellOffset[] filter = null;
            CellOffset[][] offsetTable = OffsetGroups.BuildReachabilityTable(placementOffsets, table, filter);
            SetOffsetTable(offsetTable);
            //I really don't know what this celloffset stuff is about, too afraid to delete
            //from original deconstructable class


        }
        protected override void OnSpawn()
        {
            base.OnSpawn();

        }

        protected override void OnStartWork(WorkerBase worker)
        {
            base.OnStartWork(worker);
            workElapsedTime = 0f;

            // 小人到场开始工作，撤下"待拆除"状态图标
            // Vanilla Deconstructable.OnStartWork 也做这一步
            base.GetComponent<KSelectable>().RemoveStatusItem(Db.Get().BuildingStatusItems.PendingDeconstruction, false);
            base.Trigger(1830962028, this);
        }

        // multitoolContext = "build" 下 Workable 的完成判定不靠 workTime，而是等 multitool 系统发信号。
        // 本建筑没有配全 multitool 那套参数，信号永远不来，所以这里自己累计时间并返回 true。
        // Under multitoolContext = "build", Workable does not complete based on workTime; it waits for a signal
        // from the multitool system. This building does not have the full multitool parameter set, so the signal
        // never arrives. Accumulate time here and return true to finish.
        protected override bool OnWorkTick(WorkerBase worker, float dt)
        {
            workElapsedTime += dt;
            if (workElapsedTime >= this.workTime)
            {
                return true;
            }
            return false;
        }

        protected override void OnCompleteWork(WorkerBase worker)
        {
            base.OnCompleteWork(worker);

            // 小人到场完成工作：清状态、释放优先级引用、执行拆除
            // Duplicant finished the work: clear state, release priority ref, do the deconstruct.
            deconstructChore = null;
            workElapsedTime = 0f;
            if (isMarkedForDeconstruction)
            {
                isMarkedForDeconstruction = false;
                Prioritizable.RemoveRef(base.gameObject);
            }
            DoDeconstruct();
        }

        protected override void OnCleanUp()
        {
            // 建筑被其他途径销毁（如加载存档、强制删除）时清理未完成的任务
            // Clean up any pending chore when the building is destroyed by other means (save load, force delete).
            CancelDeconstruction();
            base.OnCleanUp();
        }

        // 手动拆除入口：按钮和原版拆除事件都走这里
        //
        // 判断流程：
        //   1. 已排队 → 取消
        //   2. 先释放箱内非液体/气体的物品（"先释放其他"）
        //   3. 若箱内已无可倾倒的液体/气体，或不允许倾倒 → 瞬间拆除
        //   4. 否则（允许倾倒 + 箱内确有液体/气体）→ 排队小人任务
        //      小人到场完成后，DoDeconstruct 再处理剩下的液体/气体（"再慢慢处理气体液体"）
        //
        // Manual deconstruct entry point: both the button and the vanilla deconstruct event go through here.
        //
        // Flow:
        //   1. Already queued -> cancel
        //   2. Drop non-liquid/gas items first ("drop the rest first")
        //   3. No spillable liquid/gas left, or willSpill is off -> instant deconstruct
        //   4. Otherwise (willSpill on + liquid/gas present) -> queue a duplicant chore
        //      Once the duplicant finishes, DoDeconstruct handles the remaining liquid/gas ("then slowly handle the liquid/gas")
        public void RequestDeconstruct()
        {
            if (isMarkedForDeconstruction)
            {
                CancelDeconstruction();
                return;
            }

            HaulingPoint haulingPoint = base.GetComponent<HaulingPoint>();
            if (haulingPoint == null)
            {
                DoDeconstruct();
                return;
            }

            // 先释放其他（非液体/气体的固体等），顺便确认箱内是否还有可倾倒物
            // Drop the rest first, and while we're at it, find out whether any spillable liquid/gas remains.
            bool hasSpillable = DropNonSpillableAndCheckRemaining();

            // 无人能倒或倒不出去 → 瞬间拆除
            if (!haulingPoint.willSpill || !hasSpillable)
            {
                DoDeconstruct();
                return;
            }

            StartDeconstructChore();
        }

        // 创建小人拆除任务，并挂上"待拆除"状态图标 / 刷新菜单为"取消拆除"
        // Create the duplicant deconstruct chore, attach the "pending deconstruction" status item,
        // and refresh the menu to show "Cancel Remove".
        private void StartDeconstructChore()
        {
            isMarkedForDeconstruction = true;
            // 原版 Deconstructable / Dumpable 都在创建 chore 前 AddRef，缺失会导致 chore 无法被正常调度
            // Vanilla Deconstructable / Dumpable both AddRef before creating the chore; without it the chore will not be scheduled properly.
            Prioritizable.AddRef(base.gameObject);
            deconstructChore = new WorkChore<DeconstructableHaulingPoint>(
                Db.Get().ChoreTypes.Deconstruct,
                this,
                null,
                true,
                null,
                null,
                null,
                true,
                null,
                false,
                true,
                null,
                true,
                true,
                true,
                PriorityScreen.PriorityClass.basic,
                5,
                false,
                true);

            // 挂上"待拆除"状态图标（原版 Deconstructable.QueueDeconstruction 也这么做）
            // iconName = "status_item_pending_deconstruction"
            base.GetComponent<KSelectable>().AddStatusItem(Db.Get().BuildingStatusItems.PendingDeconstruction, this);
            base.Trigger(2108245096, "Deconstruct");

            // 刷新用户菜单，使按钮显示为"取消拆除"
            Game.Instance.userMenu.Refresh(base.gameObject);
        }

        // 取消已排队的手动拆除任务
        // Cancel a queued manual deconstruction.
        public void CancelDeconstruction()
        {
            bool wasQueued = isMarkedForDeconstruction || deconstructChore != null;
            if (!wasQueued)
            {
                // 没排过队，什么都不用做；也避免 OnCleanUp 里对正在删除的对象刷新菜单
                return;
            }

            if (deconstructChore != null)
            {
                deconstructChore.Cancel("Cancelled deconstruction");
                deconstructChore = null;

                // 撤下"待拆除"状态图标（对齐原版 Deconstructable.CancelDeconstruction）
                base.GetComponent<KSelectable>().RemoveStatusItem(Db.Get().BuildingStatusItems.PendingDeconstruction, false);
            }
            if (isMarkedForDeconstruction)
            {
                isMarkedForDeconstruction = false;
                Prioritizable.RemoveRef(base.gameObject);
            }
            workElapsedTime = 0f;

            // 刷新用户菜单，使按钮显示为"拆除"
            Game.Instance.userMenu.Refresh(base.gameObject);
        }

        // 重新评估已排队的任务：条件不再满足（不再需要小人）则取消任务并立即拆除
        // Re-evaluate a queued chore: if the condition is no longer met (no duplicant needed), cancel and deconstruct instantly.
        public void ReevaluateQueuedDeconstruction()
        {
            if (!isMarkedForDeconstruction) return;

            HaulingPoint haulingPoint = base.GetComponent<HaulingPoint>();
            if (haulingPoint == null || !haulingPoint.willSpill || !HasSpillableContent())
            {
                CancelDeconstruction();
                DoDeconstruct();
            }
        }

        // 瞬间拆除：自动拆除（存储满）时调用，不经过小人
        // 按真值表：自动拆除始终瞬间，不做"先释放其他"的分步处理，DropAll 一次性处理所有物品
        //
        // Instant deconstruct: called by auto-deconstruct (storage full); bypasses the duplicant.
        // Per the truth table: auto-deconstruct is always instant, no staged "drop the rest first" handling;
        // DropAll handles everything in one shot.
        public void InstantDeconstruct()
        {
            CancelDeconstruction();
            DoDeconstruct();
        }

        // 实际拆除逻辑，只在 DoDeconstruct / OnCompleteWork 里执行
        // 走小人任务路径时，非液体/气体物品已在 RequestDeconstruct 里先掉出，
        // 所以这里的 DropAll 实际只处理剩下的液体/气体。
        //
        // Actual deconstruct logic; only executed from DoDeconstruct / OnCompleteWork.
        // When going through the duplicant chore, non-liquid/gas items were already dropped
        // in RequestDeconstruct, so DropAll here effectively only handles the remaining liquid/gas.
        private void DoDeconstruct()
        {
            Storage storage = base.GetComponent<Storage>();
            HaulingPoint haulingPoint = base.GetComponent<HaulingPoint>();

            if (storage != null && haulingPoint != null)
            {
                storage.DropAll(haulingPoint.willSpill, haulingPoint.willSpill); //drop liquids and gasses based on setting
            }

            base.gameObject.DeleteObject(); //goodbye
        }

        // 把箱内物品分流：可倾倒的（液体/气体 + Dumpable）留下，其余立刻掉出。
        // 返回箱内是否还有可倾倒物，省掉一次额外遍历。
        //
        // Split storage contents: spillable items (liquid/gas + Dumpable) are kept; everything else is dropped now.
        // Returns whether any spillable content remains, saving an extra iteration.
        private bool DropNonSpillableAndCheckRemaining()
        {
            Storage storage = base.GetComponent<Storage>();
            if (storage == null) return false;

            // 先收集再统一 Drop，避免遍历 storage.items 时因 Drop 修改列表而出错
            // Collect first, then Drop, to avoid modifying storage.items while iterating.
            List<GameObject> toDrop = null;
            bool hasSpillable = false;

            foreach (GameObject item in storage.items)
            {
                if (item == null) continue;

                if (IsSpillable(item))
                {
                    hasSpillable = true;
                }
                else
                {
                    if (toDrop == null) toDrop = new List<GameObject>();
                    toDrop.Add(item);
                }
            }

            if (toDrop != null)
            {
                foreach (GameObject item in toDrop)
                {
                    storage.Drop(item, true);
                }
            }

            return hasSpillable;
        }

        // 箱内是否有可倾倒的液体或气体（对齐原版 Storage.DropSome 的判定）
        // 只有它们才需要"倾倒"这个动作，也才需要小人到场
        //
        // Whether the storage contains dumpable liquid or gas (aligned with vanilla Storage.DropSome).
        // Only these need the "dump" action, hence only these need a duplicant to be present.
        private bool HasSpillableContent()
        {
            Storage storage = base.GetComponent<Storage>();
            if (storage == null) return false;

            foreach (GameObject item in storage.items)
            {
                if (IsSpillable(item)) return true;
            }
            return false;
        }

        // 单项判定：必须带 Dumpable 组件，且 PrimaryElement.Element 是液体或气体
        // Per-item check: must have a Dumpable component, and its PrimaryElement.Element must be liquid or gas.
        private static bool IsSpillable(GameObject item)
        {
            if (item == null) return false;
            // 必须有 Dumpable 组件才能倾倒，与 Storage.DropSome 一致
            // Must have a Dumpable component to be dumped, same as Storage.DropSome.
            if (item.GetComponent<Dumpable>() == null) return false;
            PrimaryElement pe = item.GetComponent<PrimaryElement>();
            if (pe == null || pe.Element == null) return false;
            return pe.Element.IsLiquid || pe.Element.IsGas;
        }


        private void OnRefreshUserMenu(object data)
        {
            if (!this.HasTag(GameTags.Stored))
            {
                KIconButtonMenu.ButtonInfo button;
                if (isMarkedForDeconstruction)
                {
                    button = new KIconButtonMenu.ButtonInfo(
                        "action_deconstruct",
                        STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.CANCEL_REMOVE,
                        RequestDeconstruct,
                        Action.NumActions,
                        null, null, null,
                        STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.CANCEL_REMOVE_TOOLTIP);
                }
                else
                {
                    button = new KIconButtonMenu.ButtonInfo(
                        "action_deconstruct",
                        STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.REMOVE,
                        RequestDeconstruct,
                        Action.NumActions,
                        null, null, null,
                        STRINGS.BUILDINGS.BUTTONS.HAULINGPOINT.REMOVE_TOOLTIP);
                }
                Game.Instance.userMenu.AddButton(base.gameObject, button, 0f);
                //add deconstruct button
                //I thought about using cancel tool instead, but since it is made through build menu I thought this would be more intuivitive
            }
        }



    }
	
    public class FilteredStorageHaulingPoint
    {
        //this class is basically a copy of filteredstorage with just a few changes necessary to make hauling points work properly
        //for example, handling of forbidden tags for the auto bottler

        public static readonly HashedString FULL_PORT_ID = "FULL";

        private KMonoBehaviour root;

        private FetchList2 fetchList;

        private IUserControlledCapacity capacityControl;

        private TreeFilterable filterable;

        private Storage storage;

        private MeterController meter;

        private MeterController logicMeter;

        //private Tag[] requiredTags;

        private Tag[] forbiddenTags;

        private bool hasMeter = true;

        private bool useLogicMeter;

        private ChoreType choreType;

        public void SetHasMeter(bool has_meter)
        {
            hasMeter = has_meter;
        }

        public FilteredStorageHaulingPoint(KMonoBehaviour root, Tag[] forbidden_tags, IUserControlledCapacity capacity_control, bool use_logic_meter, ChoreType fetch_chore_type)
        {
            this.root = root;
            forbiddenTags = forbidden_tags;
            capacityControl = capacity_control;
            useLogicMeter = use_logic_meter;
            choreType = fetch_chore_type;
            root.Subscribe(-1697596308, OnStorageChanged);
            root.Subscribe(-543130682, OnUserSettingsChanged);
            filterable = root.FindOrAdd<TreeFilterable>();
            TreeFilterable treeFilterable = filterable;
            treeFilterable.OnFilterChanged = (Action<HashSet<Tag>>)Delegate.Combine(treeFilterable.OnFilterChanged, new Action<HashSet<Tag>>(OnFilterChanged));
            storage = root.GetComponent<Storage>();
            storage.Subscribe(644822890, OnOnlyFetchMarkedItemsSettingChanged);
            storage.Subscribe(-1852328367, OnFunctionalChanged);
        }

        private void OnOnlyFetchMarkedItemsSettingChanged(object data)
        {
            OnFilterChanged(filterable.GetTags());
        }

        private void CreateMeter()
        {
            if (hasMeter)
            {
                meter = new MeterController(root.GetComponent<KBatchedAnimController>(), "meter_target", "meter", Meter.Offset.Infront, Grid.SceneLayer.NoLayer, "meter_frame", "meter_level");
            }
        }

        private void CreateLogicMeter()
        {
            if (hasMeter)
            {
                logicMeter = new MeterController(root.GetComponent<KBatchedAnimController>(), "logicmeter_target", "logicmeter", Meter.Offset.Infront, Grid.SceneLayer.NoLayer);
            }
        }

        public void CleanUp()
        {
            if (filterable != null)
            {
                TreeFilterable treeFilterable = filterable;
                treeFilterable.OnFilterChanged = (Action<HashSet<Tag>>)Delegate.Remove(treeFilterable.OnFilterChanged, new Action<HashSet<Tag>>(OnFilterChanged));
            }
            if (fetchList != null)
            {
                fetchList.Cancel("Parent destroyed");
            }
        }

        public void FilterChanged()
        {
            if (hasMeter)
            {
                if (meter == null)
                {
                    CreateMeter();
                }
                if (logicMeter == null && useLogicMeter)
                {
                    CreateLogicMeter();
                }
            }
            OnFilterChanged(filterable.GetTags());
            UpdateMeter();
        }

        private void OnUserSettingsChanged(object data)
        {
            OnFilterChanged(filterable.GetTags());
            UpdateMeter();
        }

        private void OnStorageChanged(object data)
        {
            if (fetchList == null)
            {
                OnFilterChanged(filterable.GetTags());
            }
            UpdateMeter();
        }

        private void OnFunctionalChanged(object data)
        {
            OnFilterChanged(filterable.GetTags());
        }

        private void UpdateMeter()
        {
            float maxCapacityMinusStorageMargin = GetMaxCapacityMinusStorageMargin();
            float positionPercent = Mathf.Clamp01(GetAmountStored() / maxCapacityMinusStorageMargin);
            if (meter != null)
            {
                meter.SetPositionPercent(positionPercent);
            }
        }

        public bool IsFull()
        {
            float maxCapacityMinusStorageMargin = GetMaxCapacityMinusStorageMargin();
            float num = Mathf.Clamp01(GetAmountStored() / maxCapacityMinusStorageMargin);
            if (meter != null)
            {
                meter.SetPositionPercent(num);
            }
            if (!(num >= 1f))
            {
                return false;
            }
            return true;
        }

        private void OnFetchComplete()
        {
            OnFilterChanged(filterable.GetTags());
        }

        private float GetMaxCapacity()
        {
            float num = storage.capacityKg;
            if (capacityControl != null)
            {
                num = Mathf.Min(num, capacityControl.UserMaxCapacity);
            }
            return num;
        }

        private float GetMaxCapacityMinusStorageMargin()
        {
            return GetMaxCapacity() - storage.storageFullMargin;
        }

        private float GetAmountStored()
        {
            float result = storage.MassStored();
            if (capacityControl != null)
            {
                result = capacityControl.AmountStored;
            }
            return result;
        }

        private bool IsFunctional()
        {
            Operational component = storage.GetComponent<Operational>();
            if (!(component == null))
            {
                return component.IsFunctional;
            }
            return true;
        }

        public void SetForbiddenTags(Tag[] forbidden_tags)
        {
            forbiddenTags = forbidden_tags; //wouldn't need this whole class except for that
                                            //and actually, after the update 10/4 which added new public methods to modify forbidden tags, may well be entirely unnecessary
                                            //but... if it ain't broke, I'm not fixing it
        }

        private void OnFilterChanged(HashSet<Tag> tags)
        {
            bool flag = tags != null && tags.Count != 0;
            if (fetchList != null)
            {
                fetchList.Cancel("");
                fetchList = null;
            }
            float maxCapacityMinusStorageMargin = GetMaxCapacityMinusStorageMargin();
            float amountStored = GetAmountStored();
            float num = Mathf.Max(0f, maxCapacityMinusStorageMargin - amountStored);
            if (num > 0f && flag && IsFunctional())
            {
                num = Mathf.Max(0f, GetMaxCapacity() - amountStored);
                fetchList = new FetchList2(storage, choreType);
                fetchList.ShowStatusItem = false;
                fetchList.Add(tags, forbiddenTags, num, Operational.State.Functional);
                fetchList.Submit(OnFetchComplete, check_storage_contents: false);
            }
        }

        public void SetLogicMeter(bool on)
        {
            if (logicMeter != null)
            {
                logicMeter.SetPositionPercent(on ? 1f : 0f);
            }
        }
        /*public void AddForbiddenTag(Tag forbidden_tag)
		{
			if (forbiddenTags == null)
			{
				forbiddenTags = new Tag[0];
			}
			if (!forbiddenTags.Contains(forbidden_tag))
			{
				forbiddenTags = forbiddenTags.Append(forbidden_tag);
				OnFilterChanged(filterable.GetTags());
			}
		}

		public void RemoveForbiddenTag(Tag forbidden_tag)
		{
			if (forbiddenTags != null)
			{
				List<Tag> list = new List<Tag>(forbiddenTags);
				list.Remove(forbidden_tag);
				forbiddenTags = list.ToArray();
				OnFilterChanged(filterable.GetTags());
			}
		}*/
    }


}