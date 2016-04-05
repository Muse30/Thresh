using System;
using System.Collections.Generic;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using SharpDX;
using Thresh.Utility;
using HitChance = Thresh.Utility.HitChance;
using Prediction = Thresh.Utility.Prediction;
using EloBuddy.SDK.Rendering;

namespace Thresh
{
    internal class Program
    {
        private static Menu ThreshMenu, DrawingsMenu, PredictionMenu;
        private static Spell.Skillshot Q, W, E;
        public static Spell.Active Q2, R;
        public static List<AIHeroClient> Enemies = new List<AIHeroClient>(), Allies = new List<AIHeroClient>();
        static int QMana { get { return 80; } }
        static int WMana { get { return 50 * W.Level; } }
        static int EMana { get { return 60 * E.Level; } }
        static int RMana { get { return R.Level > 0 ? 100 : 0; } }


        public static AIHeroClient Player
        {
            get { return ObjectManager.Player; }
        }

        public static bool Combo
        {
            get { return (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo)); }
        }

        private static void Main(string[] args)
        {
            Loading.OnLoadingComplete += Load;
        }

        public static void Load(EventArgs args)
        {
            Chat.Print("Le Thresh XD loaded Kappa", Color.HotPink);
            if (ObjectManager.Player.Hero != Champion.Thresh) return;
            foreach (var hero in ObjectManager.Get<AIHeroClient>())
            {
                if (hero.IsEnemy)
                {
                    Enemies.Add(hero);
                }
                if (hero.IsAlly)
                    Allies.Add(hero);
            }
            Q = new Spell.Skillshot(SpellSlot.Q, 1040, SkillShotType.Linear, (int)0.5f, (int?)1900f, 70);
            Q.AllowedCollisionCount = 0;
            Q2 = new Spell.Active(SpellSlot.Q, 9000);
            W = new Spell.Skillshot(SpellSlot.W, 950, SkillShotType.Circular, 250, int.MaxValue, 10);
            W.AllowedCollisionCount = int.MaxValue;
            E = new Spell.Skillshot(SpellSlot.E, 480, SkillShotType.Linear, (int)0.25f, int.MaxValue, 50);
            E.AllowedCollisionCount = int.MaxValue;
            R = new Spell.Active(SpellSlot.R, 350);

            ThreshMenu = MainMenu.AddMenu("TDThresh", "thresh");
            ThreshMenu.Add("AACombo", new CheckBox("Disable AA if can use E"));
            ThreshMenu.Add("ts", new CheckBox("Use EB TargetSelector"));
            ThreshMenu.Add("ts1", new CheckBox("Only one target", false));
            ThreshMenu.Add("ts2", new CheckBox("All grab-able targets"));
            ThreshMenu.Add("qCC", new CheckBox("Auto Q cc & dash enemy"));
            ThreshMenu.Add("minGrab", new Slider("Min range grab", 250, 125, (int)Q.Range));
            ThreshMenu.Add("maxGrab", new Slider("Max range grab", (int)Q.Range, 125, (int)Q.Range));
            ThreshMenu.AddLabel("Grab:");
            foreach (var enemy in ObjectManager.Get<AIHeroClient>().Where(enemy => enemy.Team != Player.Team))
                ThreshMenu.Add("grab" + enemy.ChampionName, new CheckBox(enemy.ChampionName));
            ThreshMenu.AddSeparator();
            ThreshMenu.Add("GapQ", new CheckBox("Q on Gapcloser"));
            ThreshMenu.AddSeparator();
            ThreshMenu.AddGroupLabel("W SETTINGS");
            ThreshMenu.AddSeparator();
            ThreshMenu.Add("autoW", new CheckBox("Auto W"));
            ThreshMenu.Add("Wdmg", new Slider("W % hp", 10, 100, 0));
            ThreshMenu.Add("autoW2", new CheckBox("Auto W if Q Hits"));
            ThreshMenu.Add("autoW3", new CheckBox("Auto W shield dmg"));
            ThreshMenu.Add("wCount", new Slider("Auto W if x enemies near ally", 3, 0, 5));
            ThreshMenu.Add("SafeLanternKey", new KeyBind("Safe Lantern", false, KeyBind.BindTypes.HoldActive, 'H'));
            ThreshMenu.AddSeparator();
            ThreshMenu.AddGroupLabel("E SETTINGS");
            ThreshMenu.AddSeparator();
            ThreshMenu.Add("autoE", new CheckBox("Auto E"));
            ThreshMenu.Add("pushE", new CheckBox("Auto push"));
            ThreshMenu.Add("inter", new CheckBox("E Interrupt"));
            ThreshMenu.Add("Gap", new CheckBox("E on Gapcloser"));
            ThreshMenu.Add("AntiRengar", new CheckBox("Use E AntiGapCloser (Rengar Passive)"));
            ThreshMenu.Add("pullEnemy", new KeyBind("Pull Enemy", false, KeyBind.BindTypes.HoldActive, 'A'));
            ThreshMenu.Add("pushEnemy", new KeyBind("Push Enemy", false, KeyBind.BindTypes.HoldActive, 'N'));
            ThreshMenu.AddSeparator();
            ThreshMenu.AddGroupLabel("R SETTINGS");
            ThreshMenu.AddSeparator();
            ThreshMenu.Add("rCount", new Slider("Auto R if x enemies in range", 2, 0, 5));
            ThreshMenu.Add("rKs", new CheckBox("R ks", false));
            ThreshMenu.Add("comboR", new CheckBox("Always R in combo", false));

            PredictionMenu = ThreshMenu.AddSubMenu("Prediction", "prediction");
            StringList(PredictionMenu, "Qpred", "Q Prediction", new[] { "Low", "Medium", "High", "Very High" }, 3);
            StringList(PredictionMenu, "Epred", "E Prediction", new[] { "Low", "Medium", "High", "Very High" }, 1);

            DrawingsMenu = ThreshMenu.AddSubMenu("Drawings", "drawings");
            DrawingsMenu.Add("DrawTarget", new CheckBox("Draw Target"));
            DrawingsMenu.Add("qRange", new CheckBox("Q range"));
            DrawingsMenu.Add("wRange", new CheckBox("W range"));
            DrawingsMenu.Add("eRange", new CheckBox("E range"));
            DrawingsMenu.Add("rRange", new CheckBox("R range"));

            Obj_AI_Base.OnProcessSpellCast += Utils.OnProcessSpellCast;
            TickManager.Tick();
            Game.OnTick += Qcoltick;
            Orbwalker.OnPreAttack += OnPreAttack;
            Orbwalker.OnPostAttack += OnPostAttack;
            Interrupter.OnInterruptableSpell += OnInterruptable;
            Game.OnUpdate += Game_OnGameUpdate;
            Gapcloser.OnGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Interrupter.OnInterruptableSpell += Interrupter_OnInterruptableTarget;
            Drawing.OnDraw += Drawing_OnDraw;
            Obj_AI_Base.OnProcessSpellCast += Obj_AI_Base_OnProcessSpellCast;
        }

        public static void StringList(Menu menu, string uniqueId, string displayName, string[] values, int defaultValue)
        {
            var mode = menu.Add(uniqueId, new Slider(displayName, defaultValue, 0, values.Length - 1));
            mode.DisplayName = displayName + ": " + values[mode.CurrentValue];
            mode.OnValueChange +=
                delegate (ValueBase<int> sender, ValueBase<int>.ValueChangeArgs args)
                {
                    sender.DisplayName = displayName + ": " + values[args.NewValue];
                };
        }

        private static void Interrupter_OnInterruptableTarget(Obj_AI_Base sender,
            Interrupter.InterruptableSpellEventArgs args)
        {
            if (E.IsReady() && Config.inter && sender.IsValidTarget(E.Range) && sender is AIHeroClient && sender.IsEnemy)
            {
                E.Cast(sender.ServerPosition);
            }
        }
        static void Pull(Obj_AI_Base target)
        {
            var pos = target.Position.Extend(Player.Position, Player.Distance(target.Position) + 200);
            E.Cast(target);
        }

        static void Push(Obj_AI_Base target)
        {
            var pos = target.Position.Extend(Player.Position, Player.Distance(target.Position) - 200);
            E.Cast(target);
        }


        static void Obj_AI_Base_OnPlayAnimation(Obj_AI_Base sender, GameObjectPlayAnimationEventArgs args)
        {
            if (sender.IsMe)
            {
                if (Config.AntiRengar)
                    return;

                if (!(sender is AIHeroClient))
                    return;

                var _sender = sender as AIHeroClient;
                var dis = _sender.GetBuffCount("rengartrophyicon1") > 5 ? 600 : 750;

                if (_sender.ChampionName == "Rengar" && args.Animation == "Spell5" &&
                    Player.Distance(_sender.Position) < dis && E.IsReady())
                {
                    Push(_sender);
                }
            }
        }

        private static void AntiGapcloser_OnEnemyGapcloser(AIHeroClient sender, Gapcloser.GapcloserEventArgs gapcloser)
        {
            if (E.IsReady() && Config.Gap && gapcloser.Sender.IsValidTarget(E.Range) && sender.IsEnemy)
            {
                E.Cast(gapcloser.Sender);
            }
            else if (Q.IsReady() && Config.GapQ && gapcloser.Sender.IsValidTarget(Q.Range) && sender.IsEnemy && ObjectManager.Player.IsFacing(sender))
            {
                Q.Cast(gapcloser.Sender);
            }
        }

        private static bool CanUse(SpellSlot sum)
        {
            if (sum != SpellSlot.Unknown && Player.Spellbook.CanUseSpell(sum) == SpellState.Ready)
                return true;
            return false;
        }

        private static void Obj_AI_Base_OnProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            if (!sender.IsEnemy || sender.Type != GameObjectType.obj_AI_Base)
                return;


            if (sender.Distance(Player.Position) > 1600)
                return;
            var kappa = sender as AIHeroClient;

        }



        public static bool InFountain(AIHeroClient hero)
        {
            var map = Game.MapId;
            var mapIsSR = map == GameMapId.SummonersRift;
            float fountainRange = mapIsSR ? 1050 : 750;
            return hero.IsVisible &&
                   ObjectManager.Get<Obj_SpawnPoint>()
                       .Any(sp => sp.Team == hero.Team && hero.Distance(sp.Position) < fountainRange);
        }



        private static void Clean()
        {
            var qss = Player.InventoryItems.FirstOrDefault(item => item.Id == Ids.Quicksilver.Id);
            var mercurial = Player.InventoryItems.FirstOrDefault(item => item.Id == Ids.Mercurial.Id);
            var dervish = Player.InventoryItems.FirstOrDefault(item => item.Id == Ids.Dervish.Id);
            if (Ids.Quicksilver.IsReady())
                if (qss != null)
                {
                    var firstOrDefault =
                        qss.SpellSlot;
                    EloBuddy.Player.CastSpell(firstOrDefault);
                }
                else if (Ids.Mercurial.IsReady())
                    if (mercurial != null)
                    {
                        var firstOrDefault =
                            mercurial.SpellSlot;
                        EloBuddy.Player.CastSpell(firstOrDefault);
                    }
                    else if (Ids.Dervish.IsReady())
                        if (dervish != null)
                        {
                            var firstOrDefault =
                                dervish.SpellSlot;
                            EloBuddy.Player.CastSpell(firstOrDefault);
                        }
        }



        protected static void OnInterruptable(Obj_AI_Base sender,
            Interrupter.InterruptableSpellEventArgs args)
        {


        }



        private static void PotionManagement()
        {
            if (Player.HasBuff("RegenerationPotion") || Player.HasBuff("ItemMiniRegenPotion") ||
                Player.HasBuff("ItemCrystalFlaskJungle") || Player.HasBuff("ItemDarkCrystalFlask"))
                return;

            if (Ids.Potion.IsReady())
            {
                var inventorySlot = Player.InventoryItems.FirstOrDefault(item => item.Id == ItemId.Health_Potion);
                if (Player.Health + 200 < Player.MaxHealth && Player.CountEnemiesInRange(700) > 0)
                {
                    if (inventorySlot != null)
                    {
                        var firstOrDefault =
                            inventorySlot.SpellSlot;
                        EloBuddy.Player.CastSpell(firstOrDefault);
                    }
                }
                else if (Player.Health < Player.MaxHealth * 0.6)
                    if (inventorySlot != null)
                    {
                        var firstOrDefault =
                            inventorySlot.SpellSlot;
                        EloBuddy.Player.CastSpell(firstOrDefault);
                    }
            }
            else if (Ids.Biscuit.IsReady())
            {
                var inventorySlot = Player.InventoryItems.FirstOrDefault(item => item.Id == Ids.Biscuit.Id);
                if (Player.Health + 250 < Player.MaxHealth && Player.CountEnemiesInRange(700) > 0)
                    if (inventorySlot != null)
                    {
                        var firstOrDefault =
                            inventorySlot.SpellSlot;
                        EloBuddy.Player.CastSpell(firstOrDefault);
                    }
                if (Player.Health < Player.MaxHealth * 0.6)
                    if (inventorySlot != null)
                    {
                        var firstOrDefault =
                            inventorySlot.SpellSlot;
                        EloBuddy.Player.CastSpell(firstOrDefault);
                    }
            }
            else if (Ids.Hunter.IsReady())
            {
                var inventorySlot = Player.InventoryItems.FirstOrDefault(item => item.Id == Ids.Hunter.Id);
                if (Player.Health + 250 < Player.MaxHealth && Player.CountEnemiesInRange(700) > 0)
                    if (inventorySlot != null)
                    {
                        var firstOrDefault =
                            inventorySlot.SpellSlot;
                        EloBuddy.Player.CastSpell(firstOrDefault);
                    }
                if (Player.Health < Player.MaxHealth * 0.6)
                    if (inventorySlot != null)
                    {
                        var firstOrDefault =
                            inventorySlot.SpellSlot;
                        EloBuddy.Player.CastSpell(firstOrDefault);
                    }
            }
            else if (Ids.Corrupting.IsReady())
            {
                var inventorySlot = Player.InventoryItems.FirstOrDefault(item => item.Id == Ids.Corrupting.Id);
                if (Player.Health + 250 < Player.MaxHealth && Player.CountEnemiesInRange(700) > 0)
                    if (inventorySlot != null)
                    {
                        var firstOrDefault =
                            inventorySlot.SpellSlot;
                        EloBuddy.Player.CastSpell(firstOrDefault);
                    }
                if (Player.Health < Player.MaxHealth * 0.6)
                    if (inventorySlot != null)
                    {
                        var firstOrDefault =
                            inventorySlot.SpellSlot;
                        EloBuddy.Player.CastSpell(firstOrDefault);
                    }
            }
            else if (Ids.Refillable.IsReady())
            {
                var inventorySlot = Player.InventoryItems.FirstOrDefault(item => item.Id == Ids.Refillable.Id);
                if (Player.Health + 250 < Player.MaxHealth && Player.CountEnemiesInRange(700) > 0)
                    if (inventorySlot != null)
                    {
                        var firstOrDefault =
                            inventorySlot.SpellSlot;
                        EloBuddy.Player.CastSpell(firstOrDefault);
                    }
                if (Player.Health < Player.MaxHealth * 0.6)
                    if (inventorySlot != null)
                    {
                        var firstOrDefault =
                            inventorySlot.SpellSlot;
                        EloBuddy.Player.CastSpell(firstOrDefault);
                    }
            }
        }

        private static void Game_OnGameUpdate(EventArgs args)
        {
            if (Player.IsRecalling() || Player.IsDead)
                return;


            if (Combo && Config.AACombo)
            {
                if (!E.IsReady())
                    Orbwalker.DisableAttacking = false;

                else
                    Orbwalker.DisableAttacking = true;
            }
            else
                Orbwalker.DisableAttacking = false;


            if (Q.IsReady())
                LogicQ();
            if (E.IsReady() && Config.autoE)
                LogicE();
            if (W.IsReady())
                LogicW();
            if (R.IsReady())
                LogicR();
            if (Config.Push)
            {
                Push();
            }
            if (Config.Pull)
            {
                Pull();
            }
        }

        private static void LogicE()
        {
            var t = TargetSelector.GetTarget(E.Range, DamageType.Physical);
            if (t.IsValidTarget() && !t.HasBuff("ThreshQ") && Utils.CanMove(t))
            {
                if (Combo)
                {
                    CastE(false, t);
                }
                else if (Config.pushE)
                {
                    CastE(true, t);
                }

            }
        }

        private static void LogicQ()
        {
            foreach (
                var enemy in
                    Enemies.Where(
                        enemy =>
                            enemy.IsValidTarget(Q.Range + 300) && enemy.HasBuff("ThreshQ") && !enemy.IsMinion &&
                            !enemy.IsMonster))
            {
                if (Combo)
                {
                    if (W.IsReady() && Config.autoW2)
                    {
                        foreach (
                            var ally in
                                Allies.Where(
                                    ally =>
                                        ally.IsValid && !ally.IsDead &&
                                        Player.Distance(ally.ServerPosition) < W.Range + 500))
                        {
                            if (enemy.Distance(ally.ServerPosition) > 800 && Player.Distance(ally.ServerPosition) > 600)
                            {
                                CastW(W.GetPrediction(ally).CastPosition);
                            }
                        }
                    }

                    if (Utils.GetPassiveTime(enemy, "ThreshQ") < 0.4)
                        Q2.Cast();
                }
                return;
            }

            if (Combo && Config.ts)
            {
                var t = TargetSelector.GetTarget(Config.maxGrab, DamageType.Physical);

                if (t.IsValidTarget(Config.maxGrab) && !t.HasBuffOfType(BuffType.SpellImmunity) &&
                    !t.HasBuffOfType(BuffType.SpellShield) && ThreshMenu["grab" + t.ChampionName].Cast<CheckBox>().CurrentValue && Player.Distance(t.ServerPosition) > Config.minGrab)
                    CastSpell(Q, t, predQ(), Config.maxGrab);
            }


            foreach (var t in Enemies.Where(t => t.IsValidTarget(Config.maxGrab) && ThreshMenu["grab" + t.ChampionName].Cast<CheckBox>().CurrentValue))
            {
                if (!t.HasBuffOfType(BuffType.SpellImmunity) && !t.HasBuffOfType(BuffType.SpellShield) &&
                    Player.Distance(t.ServerPosition) > Config.minGrab)
                {
                    if (Combo && !Config.ts)
                        CastSpell(Q, t, predQ(), Config.maxGrab);

                    if (Config.qCC)
                    {
                        if (!Utils.CanMove(t))
                            Q.Cast(t);
                        var pred = Q.GetPrediction(t);
                        if (pred.HitChance == (EloBuddy.SDK.Enumerations.HitChance)HitChance.Dashing)
                        {
                            Q.Cast(t);
                        }
                        if (pred.HitChance == (EloBuddy.SDK.Enumerations.HitChance)HitChance.Immobile)
                        {
                            Q.Cast(t);
                        }
                    }
                }
            }
        }

        private static void Pull()
        {
            var target = TargetSelector.GetTarget(E.Range, DamageType.Magical);
            Orbwalker.OrbwalkTo(Game.CursorPos.Extend(Game.CursorPos, 200).To3D());

            if (E.IsReady() && Player.Distance(target.Position) < E.Range)
            {
                CastSpell(E, target, predE(), (int)E.Range);
            }
        }

        private static void Push()
        {
            var target = TargetSelector.GetTarget(E.Range, DamageType.Magical);
            Orbwalker.OrbwalkTo(Game.CursorPos.Extend(Game.CursorPos, 200).To3D());
            if (E.IsReady() && Player.Distance(target.Position) < E.Range)
            {
                E.Cast(target.Position);
            }
        }
        private static bool collision;
        private static void Qcoltick(EventArgs args)
        {
            if (Orbwalker.LastTarget != null && Orbwalker.LastTarget is AIHeroClient)
            {
                var target = Orbwalker.LastTarget as AIHeroClient;
                var pred = Q.GetPrediction(target);
                if (pred.CollisionObjects.Any())
                {
                    collision = true;
                }
                else
                {
                    collision = false;
                }
            }
        }
        internal static bool IsAutoAttacking;
        private static void CastSpell(Spell.Skillshot QWER, Obj_AI_Base target, HitChance hitchance, int MaxRange)
        {
            var coreType2 = SkillshotType.SkillshotLine;
            var aoe2 = false;
            if ((int)QWER.Type == (int)SkillshotType.SkillshotCircle)
            {
                coreType2 = SkillshotType.SkillshotCircle;
                aoe2 = true;
            }
            if (QWER.Width > 80 && QWER.AllowedCollisionCount < 100)
                aoe2 = true;
            var predInput2 = new PredictionInput
            {
                Aoe = aoe2,
                Collision = QWER.AllowedCollisionCount < 100,
                Speed = QWER.Speed,
                Delay = QWER.CastDelay,
                Range = MaxRange,
                From = Player.ServerPosition,
                Radius = QWER.Radius,
                Unit = target,
                Type = coreType2
            };
            var poutput2 = Prediction.GetPrediction(predInput2);
            if (QWER.Speed < float.MaxValue && Utils.CollisionYasuo(Player.ServerPosition, poutput2.CastPosition))
                return;

            if (hitchance == HitChance.VeryHigh)
            {
                if (poutput2.Hitchance >= HitChance.VeryHigh)
                    QWER.Cast(poutput2.CastPosition);
                else if (predInput2.Aoe && poutput2.AoeTargetsHitCount > 1 &&
                         poutput2.Hitchance >= HitChance.High)
                {
                    QWER.Cast(poutput2.CastPosition);
                }
            }
            else if (hitchance == HitChance.High)
            {
                if (poutput2.Hitchance >= HitChance.High)
                    QWER.Cast(poutput2.CastPosition);
            }
            else if (hitchance == HitChance.Medium)
            {
                if (poutput2.Hitchance >= HitChance.Medium)
                    QWER.Cast(poutput2.CastPosition);
            }
            else if (hitchance == HitChance.Low)
            {
                if (poutput2.Hitchance >= HitChance.Low)
                    QWER.Cast(poutput2.CastPosition);
            }
        }

        protected static void OnPostAttack(AttackableUnit target, EventArgs args)
        {
            IsAutoAttacking = false;
        }

        protected static void OnPreAttack(AttackableUnit target, Orbwalker.PreAttackArgs args)
        {
            IsAutoAttacking = true;
        }

        public static float RDamage(Obj_AI_Base target)
        {
            return ObjectManager.Player.CalculateDamageOnUnit(target, DamageType.Physical,
                (float)
                    (new double[] { 80, 120, 160, 200, 240 }[
                        ObjectManager.Player.Spellbook.GetSpell(SpellSlot.R).Level - 1]
                     + 1 * (ObjectManager.Player.TotalMagicalDamage)));
        }

        private static void LogicR()
        {
            var rKs = Config.rKs;
            foreach (
                var target in Enemies.Where(target => target.IsValidTarget(R.Range) && target.HasBuff("rocketgrab2")))
            {
                if (rKs && RDamage(target) > target.Health)
                    R.Cast();
            }
            if (Player.CountEnemiesInRange(R.Range) >= Config.rCount && Config.rCount > 0)
                R.Cast();
            if (Config.comboR)
            {
                var t = TargetSelector.GetTarget(E.Range, DamageType.Physical);
                if (t.IsValidTarget() && ((Player.UnderTurret(false) && !Player.UnderTurret(true)) || Combo))
                {
                    if (ObjectManager.Player.Distance(t.ServerPosition) > ObjectManager.Player.Distance(t.Position))
                        R.Cast();
                }
            }
        }

        private static void CastW(Vector3 pos)
        {
            if (Player.Distance(pos) < W.Range)
                W.Cast(pos);
            else
                W.Cast(Player.Position.ExtendVector3(pos, W.Range));
        }

        private static void LogicW()
        {
            if (Allies.Any())
                foreach (var ally in Allies.Where(ally => ally.IsValid && !ally.IsDead && Player.Distance(ally) < W.Range))
                {
                    var dmg = Utils.GetIncomingDamage(ally);
                    var nearEnemys = ally.CountEnemiesInRange(900);
                    var HpPercentage = (dmg * 100) / ally.Health;
                    if (nearEnemys >= Config.wCount && HpPercentage >= Config.wCount && Config.wCount > 0)
                        CastW(W.GetPrediction(ally).CastPosition);

                    if (Config.autoW)
                    {
                        var dmg2 = Utils.GetIncomingDamage(ally);
                        if (dmg2 == 0)
                            continue;

                        var sensitivity = 20;

                        var HpPercentage1 = (dmg2 * 100) / ally.Health;
                        var shieldValue = 20 + (Player.Level * 20) + (0.4 * Player.FlatMagicDamageMod);

                        nearEnemys = (nearEnemys == 0) ? 1 : nearEnemys;

                        if (dmg > shieldValue && Config.autoW3)
                            W.Cast(W.GetPrediction(ally).CastPosition);
                        else if (dmg > 100 + Player.Level * sensitivity)
                            W.Cast(W.GetPrediction(ally).CastPosition);
                        else if (ally.Health - dmg < nearEnemys * ally.Level * sensitivity)
                            W.Cast(W.GetPrediction(ally).CastPosition);
                        else if (HpPercentage1 >= Config.Wdmg)
                            W.Cast(W.GetPrediction(ally).CastPosition);
                    }
                }
        }


        private static void CastE(bool pull, AIHeroClient target)
        {
            var coreType2 = SkillshotType.SkillshotLine;
            var aoe2 = false;
            if ((int)E.Type == (int)SkillshotType.SkillshotCircle)
            {
                coreType2 = SkillshotType.SkillshotCircle;
                aoe2 = true;
            }
            if (E.Width > 80 && E.AllowedCollisionCount < 100)
                aoe2 = true;
            var predInput2 = new PredictionInput
            {
                Aoe = aoe2,
                Collision = E.AllowedCollisionCount < 100,
                Speed = E.Speed,
                Delay = E.CastDelay,
                Range = E.Range,
                From = Player.ServerPosition,
                Radius = E.Radius,
                Unit = target,
                Type = coreType2
            };
            var eprediction = Utility.Prediction.GetPrediction(predInput2);
            if (pull && eprediction.Hitchance >= predE())
            {
                CastSpell(E, target, predE(), (int)E.Range);
            }
            else
            {
                var position = Player.ServerPosition - (eprediction.CastPosition - Player.ServerPosition);
                E.Cast(position);
            }
        }

        private static HitChance predQ()
        {
            switch (Config.Qpred)
            {
                case 0:
                    return HitChance.Low;
                case 1:
                    return HitChance.Medium;
                case 2:
                    return HitChance.High;
                case 3:
                    return HitChance.VeryHigh;
            }
            return HitChance.Medium;
        }

        private static HitChance predE()
        {
            switch (Config.Epred)
            {
                case 0:
                    return HitChance.Low; ;
                case 1:
                    return HitChance.Medium;
                case 2:
                    return HitChance.High;
                case 3:
                    return HitChance.VeryHigh;
            }
            return HitChance.Medium;
        }

        private static void Drawing_OnDraw(EventArgs args)

        {
            var target = TargetSelector.GetTarget(Q.Range, DamageType.Magical);
            if (Config.qRange)
            {
                if (Config.onlyRdy)
                {
                    if (Q.IsReady())

                        Circle.Draw(Color.Blue, Config.maxGrab, ObjectManager.Player.Position);
                }
                else
                    Circle.Draw(Color.Blue, Config.maxGrab, ObjectManager.Player.Position);
            }

            if (Config.wRange)
            {
                if (Config.onlyRdy)
                {
                    if (E.IsReady())
                        Circle.Draw(Color.Red, W.Range, ObjectManager.Player.Position);

                }
                else
                    Circle.Draw(Color.Red, W.Range, ObjectManager.Player.Position);
            }

            if (Config.DrawTarget && target != null)
            {
                Drawing.DrawCircle(target.Position, 50, System.Drawing.Color.Red);
            }

            if (Config.eRange)
            {
                if (Config.onlyRdy)
                {
                    if (E.IsReady())
                        Circle.Draw(Color.Orange, E.Range, ObjectManager.Player.Position);
                }
                else
                    Circle.Draw(Color.Orange, E.Range, ObjectManager.Player.Position);
            }

            if (Config.rRange)
            {
                if (Config.onlyRdy)
                {
                    if (R.IsReady())
                        Circle.Draw(Color.Magenta, R.Range, ObjectManager.Player.Position);
                }
                else
                    Circle.Draw(Color.Magenta, R.Range, ObjectManager.Player.Position);
            }
        }

        public static class Ids
        {
            //Cleans
            public static readonly Item Mikaels = new Item(3222, 600f);
            public static readonly Item Quicksilver = new Item(3140);
            public static readonly Item Mercurial = new Item(3139);
            public static readonly Item Dervish = new Item(3137);
            //REGEN
            public static readonly Item Potion = new Item(2003);
            public static readonly Item ManaPotion = new Item(2004);
            public static readonly Item Flask = new Item(204);
            public static readonly Item Biscuit = new Item(2010);
            public static readonly Item Refillable = new Item(2031);
            public static readonly Item Hunter = new Item(2032);
            public static readonly Item Corrupting = new Item(2033);
            //def
            public static readonly Item FaceOfTheMountain = new Item(ItemId.Face_of_the_Mountain, 600f);
            public static readonly Item Zhonya = new Item(3157);
            public static readonly Item Seraph = new Item(3040);
            public static readonly Item Solari = new Item(3190, 600f);
            public static readonly Item Randuin = new Item(3143, 400f);
        }

        public static class Config
        {
            public static bool AACombo
            {
                get { return ThreshMenu["AACombo"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool ts
            {
                get { return ThreshMenu["ts"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool ts1
            {
                get { return ThreshMenu["ts1"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool ts2
            {
                get { return ThreshMenu["ts2"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool qCC
            {
                get { return ThreshMenu["qCC"].Cast<CheckBox>().CurrentValue; }
            }

            public static int minGrab
            {
                get { return ThreshMenu["minGrab"].Cast<Slider>().CurrentValue; }
            }

            public static int maxGrab
            {
                get { return ThreshMenu["maxGrab"].Cast<Slider>().CurrentValue; }
            }

            public static bool GapQ
            {
                get { return ThreshMenu["GapQ"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool autoW
            {
                get { return ThreshMenu["autoW"].Cast<CheckBox>().CurrentValue; }
            }

            public static int Wdmg
            {
                get { return ThreshMenu["Wdmg"].Cast<Slider>().CurrentValue; }
            }

            public static bool autoW2
            {
                get { return ThreshMenu["autoW2"].Cast<CheckBox>().CurrentValue; }
            }
            public static bool DrawTarget
            {
                get { return DrawingsMenu["DrawTarget"].Cast<CheckBox>().CurrentValue; }
            }


            public static bool autoW3
            {
                get { return ThreshMenu["autoW3"].Cast<CheckBox>().CurrentValue; }
            }

            public static int wCount
            {
                get { return ThreshMenu["wCount"].Cast<Slider>().CurrentValue; }
            }

            public static bool AntiRengar
            {
                get { return ThreshMenu["AntiRengar"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool Pull
            {
                get { return ThreshMenu["pullEnemy"].Cast<KeyBind>().CurrentValue; }
            }

            public static bool Push
            {
                get { return ThreshMenu["pushEnemy"].Cast<KeyBind>().CurrentValue; }
            }

            public static bool autoE
            {
                get { return ThreshMenu["autoE"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool pushE
            {
                get { return ThreshMenu["pushE"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool inter
            {
                get { return ThreshMenu["inter"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool Gap
            {
                get { return ThreshMenu["Gap"].Cast<CheckBox>().CurrentValue; }
            }

            public static int rCount
            {
                get { return ThreshMenu["rCount"].Cast<Slider>().CurrentValue; }
            }

            public static bool rKs
            {
                get { return ThreshMenu["rKs"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool comboR
            {
                get { return ThreshMenu["comboR"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool qRange
            {
                get { return DrawingsMenu["qRange"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool wRange
            {
                get { return DrawingsMenu["wRange"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool eRange
            {
                get { return DrawingsMenu["eRange"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool rRange
            {
                get { return DrawingsMenu["rRange"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool onlyRdy
            {
                get { return DrawingsMenu["onlyRdy"].Cast<CheckBox>().CurrentValue; }
            }

            public static int Qpred
            {
                get { return PredictionMenu["Qpred"].Cast<Slider>().CurrentValue; }
            }

            public static int Epred
            {
                get { return PredictionMenu["Epred"].Cast<Slider>().CurrentValue; }
            }
        }
    }
}