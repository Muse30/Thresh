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
using Thresh.Utils;
using Color = System.Drawing.Color;
using HitChance = Thresh.Utils.HitChance;
using Prediction = Thresh.Utils.Prediction;

namespace Thresh
{
    public static class Program
    {
        private static Menu ThreshMenu, Menu1, Drawz, Prediciontz, Activator, KMenu;
        private static Spell.Skillshot Q, W, E;
        public static Spell.Active Q2, R;
        public static List<AIHeroClient> Enemies = new List<AIHeroClient>(), Allies = new List<AIHeroClient>();
        private static SpellSlot exhaust, ignite, heal;
        private static int grab = 0, grabS = 0;
        private static float grabW = 0;
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
            Q2 = new Spell.Active(SpellSlot.Q, 9000);// kappa
            W = new Spell.Skillshot(SpellSlot.W, 950, SkillShotType.Circular, 250, int.MaxValue, 10);
            W.AllowedCollisionCount = int.MaxValue;
            E = new Spell.Skillshot(SpellSlot.E, 480, SkillShotType.Linear, (int)0.25f, int.MaxValue, 50);
            E.AllowedCollisionCount = int.MaxValue;
            R = new Spell.Active(SpellSlot.R, 350);

            ThreshMenu = MainMenu.AddMenu("Thresh Port", "thresh");

            Prediciontz = ThreshMenu.AddSubMenu("Prediction", "prediction");
            StringList(Prediciontz, "Qprediction", "Q Prediction", new[] { "Low", "Medium", "High", "Very High" }, 1);
            StringList(Prediciontz, "Eprediction", "E Prediction", new[] { "Low", "Medium", "High", "Very High" }, 1);


            Menu1 = ThreshMenu.AddSubMenu("Combo", "q");
            Menu1.Add("AACombo", new CheckBox("Disable AA if E in range"));
            Menu1.Add("ts", new CheckBox("Use EB TargetSelector"));
            Menu1.Add("ts1", new CheckBox("Only selected target", false));
            Menu1.Add("ts2", new CheckBox("All grab-able targets"));
            Menu1.Add("qCC", new CheckBox("Auto Q cc & dash enemy"));
            Menu1.Add("minGrab", new Slider("Min range for Q", 250, 125, (int)Q.Range));
            Menu1.Add("maxGrab", new Slider("Max range for Q", (int)Q.Range, 125, (int)Q.Range));
            Menu1.AddLabel("Q:");
            foreach (var enemy in ObjectManager.Get<AIHeroClient>().Where(enemy => enemy.Team != Player.Team))
                Menu1.Add("grab" + enemy.ChampionName, new CheckBox(enemy.ChampionName));
            Menu1.AddSeparator();
            Menu1.Add("GapQ", new CheckBox("Q on Gapcloser"));
            Menu1.AddGroupLabel("W SETTINGS");
            Menu1.AddSeparator();
            Menu1.Add("autoW", new CheckBox("Auto W"));
            Menu1.Add("Wdmg", new Slider("W on % hp", 10, 100, 0));
            Menu1.Add("autoW2", new CheckBox("Auto W if Q Hits"));
            Menu1.Add("autoW3", new CheckBox("Auto W shield dmg"));
            Menu1.Add("UseSafeLantern", new CheckBox("Use SafeLantern for our team"));
            Menu1.Add("wCount", new Slider("Auto W if x enemies near ally", 2, 0, 5));
            Menu1.Add("SafeLanternKey", new KeyBind("Safe Lantern", false, KeyBind.BindTypes.HoldActive, 'H'));
            Menu1.AddGroupLabel("E SETTINGS");
            Menu1.AddSeparator();
            Menu1.Add("autoE", new CheckBox("Auto E"));
            Menu1.Add("pushE", new CheckBox("Auto push"));
            Menu1.Add("inter", new CheckBox("OnPossibleToInterrupt"));
            Menu1.Add("Gap", new CheckBox("OnEnemyGapcloser"));
            Menu1.Add("AntiRengar", new CheckBox("Use E AntiGapCloser (Rengar Passive)"));
            Menu1.Add("pullEnemy", new KeyBind("Pull Enemy", false, KeyBind.BindTypes.HoldActive, 'A'));
            Menu1.Add("pushEnemy", new KeyBind("Push Enemy", false, KeyBind.BindTypes.HoldActive, 'N'));
            Menu1.AddGroupLabel("R SETTINGS");
            Menu1.AddSeparator();
            Menu1.Add("rCount", new Slider("Auto R if x enemies ", 2, 0, 5));
            Menu1.Add("rKs", new CheckBox("R ks", false));
            Menu1.Add("comboR", new CheckBox("Always R in combo", false));

            Drawz = ThreshMenu.AddSubMenu("Drawings", "drawings");
            Drawz.Add("DrawTarget", new CheckBox("Draw Target"));
            Drawz.Add("qRange", new CheckBox("Q range"));
            Drawz.Add("wRange", new CheckBox("W range"));
            Drawz.Add("eRange", new CheckBox("E range"));
            Drawz.Add("rRange", new CheckBox("R range"));
            Drawz.Add("onlyRdy", new CheckBox("Draw when skill rdy"));


            Orbwalker.OnPreAttack += OnPreAttack;
            Orbwalker.OnPostAttack += OnPostAttack;
            Game.OnUpdate += Game_OnGameUpdate;
            Gapcloser.OnGapcloser += AntiGapcloser_OnEnemyGapcloser;
            Interrupter.OnInterruptableSpell += Interrupter_OnInterruptableTarget;
            Drawing.OnDraw += Drawing_OnDraw;
            Obj_AI_Base.OnProcessSpellCast += Utils2.OnProcessSpellCast;
            TickManager.Tick();
            Game.OnTick += Qcoltick;

        }

        public static class Config
        {
            public static bool AACombo
            {
                get { return Menu1["AACombo"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool ts
            {
                get { return Menu1["ts"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool ts1
            {
                get { return Menu1["ts1"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool ts2
            {
                get { return Menu1["ts2"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool qCC
            {
                get { return Menu1["qCC"].Cast<CheckBox>().CurrentValue; }
            }

            public static int minGrab
            {
                get { return Menu1["minGrab"].Cast<Slider>().CurrentValue; }
            }

            public static int maxGrab
            {
                get { return Menu1["maxGrab"].Cast<Slider>().CurrentValue; }
            }

            public static bool GapQ
            {
                get { return Menu1["GapQ"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool autoW
            {
                get { return Menu1["autoW"].Cast<CheckBox>().CurrentValue; }
            }

            public static int Wdmg
            {
                get { return Menu1["Wdmg"].Cast<Slider>().CurrentValue; }
            }

            public static bool autoW2
            {
                get { return Menu1["autoW2"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool autoW3
            {
                get { return Menu1["autoW3"].Cast<CheckBox>().CurrentValue; }
            }

            public static int wCount
            {
                get { return Menu1["wCount"].Cast<Slider>().CurrentValue; }
            }

            public static bool autoE
            {
                get { return Menu1["autoE"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool pushE
            {
                get { return Menu1["pushE"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool inter
            {
                get { return Menu1["inter"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool Gap
            {
                get { return Menu1["Gap"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool UseSafeLantern
            {
                get { return Menu1["UseSafeLantern"].Cast<CheckBox>().CurrentValue; }
            }


            public static bool AntiRengar
            {
                get { return Menu1["AntiRengar"].Cast<CheckBox>().CurrentValue; }
            }

            public static int rCount
            {
                get { return Menu1["rCount"].Cast<Slider>().CurrentValue; }
            }

            public static bool rKs
            {
                get { return Menu1["rKs"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool comboR
            {
                get { return Menu1["comboR"].Cast<CheckBox>().CurrentValue; }
            }
            public static bool SafeLanternKey
            {
                get { return Menu1["SafeLanternKey"].Cast<KeyBind>().CurrentValue; }
            }

            public static bool Pull
            {
                get { return Menu1["pullEnemy"].Cast<KeyBind>().CurrentValue; }
            }

            public static bool Push
            {
                get { return Menu1["pushEnemy"].Cast<KeyBind>().CurrentValue; }
            }
            public static bool qRange
            {
                get { return Drawz["qRange"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool wRange
            {
                get { return Drawz["wRange"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool eRange
            {
                get { return Drawz["eRange"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool rRange
            {
                get { return Drawz["rRange"].Cast<CheckBox>().CurrentValue; }
            }
            public static bool DrawTarget
            {
                get { return Drawz["DrawTarget"].Cast<CheckBox>().CurrentValue; }
            }

            public static bool onlyRdy
            {
                get { return Drawz["onlyRdy"].Cast<CheckBox>().CurrentValue; }
            }

            public static int Qpred
            {
                get { return Prediciontz["Qpred"].Cast<Slider>().CurrentValue; }
            }

            public static int Epred
            {
                get { return Prediciontz["Epred"].Cast<Slider>().CurrentValue; }
            }
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

        static bool ManaManager()
        {
            var status = false;
            var ReqMana = R.IsReady() ? QMana + EMana + RMana : QMana + EMana;

            if (ReqMana < Player.Mana)
            {
                status = true;
            }
            else if (Player.MaxHealth * 0.3 > Player.Health)
            {
                status = true;
            }

            return status;
        }

        static void SafeLantern()
        {
            if (!ManaManager())
                return;

            foreach (var hero in ObjectManager.Get<AIHeroClient>()
                .Where(x => x.IsAlly && !x.IsDead && !x.IsMe &&
                Player.Distance(x.Position) < 1500 &&
                !x.HasBuff("Recall")))
            {
                if (Player.HealthPercent < 25)
                {
                    if (Player.Distance(hero.Position) <= W.Range)
                    {
                        var Pos = W.GetPrediction(hero).CastPosition;

                        CastW(Pos);
                    }
                }
                else if (hero.HasBuffOfType(BuffType.Suppression) ||
                    hero.HasBuffOfType(BuffType.Taunt) ||
                    hero.HasBuffOfType(BuffType.Knockup) ||
                    hero.HasBuffOfType(BuffType.Flee))
                {
                    if (Player.Distance(hero.Position) <= W.Range)
                    {
                        CastW(hero.Position);
                    }
                }
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

        private static bool CanUse(SpellSlot sum)
        {
            if (sum != SpellSlot.Unknown && Player.Spellbook.CanUseSpell(sum) == SpellState.Ready)
                return true;
            return false;
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

        public static bool InFountain(AIHeroClient hero)
        {
            var map = Game.MapId;
            var mapIsSR = map == GameMapId.SummonersRift;
            float fountainRange = mapIsSR ? 1050 : 750;
            return hero.IsVisible &&
                   ObjectManager.Get<Obj_SpawnPoint>()
                       .Any(sp => sp.Team == hero.Team && hero.Distance(sp.Position) < fountainRange);
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
            if (t.IsValidTarget() && !t.HasBuff("ThreshQ") && Utils2.CanMove(t))
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

                    if (Utils2.GetPassiveTime(enemy, "ThreshQ") < 0.4)
                        Q2.Cast();
                }
                return;
            }

            if (Combo && Config.ts)
            {
                var t = TargetSelector.GetTarget(Config.maxGrab, DamageType.Physical);

                if (t.IsValidTarget(Config.maxGrab) && !t.HasBuffOfType(BuffType.SpellImmunity) &&
                    !t.HasBuffOfType(BuffType.SpellShield) && Menu1["grab" + t.ChampionName].Cast<CheckBox>().CurrentValue && Player.Distance(t.ServerPosition) > Config.minGrab)
                    CastSpell(Q, t, predQ(), Config.maxGrab);
            }


            foreach (var t in Enemies.Where(t => t.IsValidTarget(Config.maxGrab) && Menu1["grab" + t.ChampionName].Cast<CheckBox>().CurrentValue))
            {
                if (!t.HasBuffOfType(BuffType.SpellImmunity) && !t.HasBuffOfType(BuffType.SpellShield) &&
                    Player.Distance(t.ServerPosition) > Config.minGrab)
                {
                    if (Combo && !Config.ts)
                        CastSpell(Q, t, predQ(), Config.maxGrab);

                    if (Config.qCC)
                    {
                        if (!Utils2.CanMove(t))
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
            if (QWER.Speed < float.MaxValue && Utils2.CollisionYasuo(Player.ServerPosition, poutput2.CastPosition))
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

        private static void OnPostAttack(AttackableUnit target, EventArgs args)
        {
            IsAutoAttacking = false;
        }

        private static void OnPreAttack(AttackableUnit target, Orbwalker.PreAttackArgs args)
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
                    var nearEnemys = ally.CountEnemiesInRange(900);

                    if (nearEnemys >= Config.wCount && Config.wCount > 0)
                        CastW(W.GetPrediction(ally).CastPosition);

                    if (Config.autoW)
                    {
                        var dmg = Utils2.GetIncomingDamage(ally);
                        if (dmg == 0)
                            continue;

                        var sensitivity = 20;

                        var HpPercentage = (dmg * 100) / ally.Health;
                        var shieldValue = 20 + (Player.Level * 20) + (0.4 * Player.FlatMagicDamageMod);

                        nearEnemys = (nearEnemys == 0) ? 1 : nearEnemys;

                        if (dmg > shieldValue && Config.autoW3)
                            W.Cast(W.GetPrediction(ally).CastPosition);
                        else if (dmg > 100 + Player.Level * sensitivity)
                            W.Cast(W.GetPrediction(ally).CastPosition);
                        else if (ally.Health - dmg < nearEnemys * ally.Level * sensitivity)
                            W.Cast(W.GetPrediction(ally).CastPosition);
                        else if (HpPercentage >= Config.Wdmg)
                            W.Cast(W.GetPrediction(ally).CastPosition);
                    }
                }
        }


        static void SafeLanternKeybind()
        {
            AIHeroClient Wtarget = null;
            float Hp = 0;

            foreach (var hero in ObjectManager.Get<AIHeroClient>()
                .Where(x => x.IsAlly && !x.IsDead && !x.IsMe &&
                Player.Distance(x.Position) < 1500 &&
                !x.HasBuff("Recall")))
            {
                var temp = hero.HealthPercent;

                if (hero.HasBuffOfType(BuffType.Suppression) ||
                    hero.HasBuffOfType(BuffType.Taunt) ||
                    hero.HasBuffOfType(BuffType.Knockup) ||
                    hero.HasBuffOfType(BuffType.Stun) ||
                    hero.HasBuffOfType(BuffType.Slow) ||
                    hero.HasBuffOfType(BuffType.Flee))
                {
                    if (Player.Distance(hero.Position) <= W.Range)
                    {
                        CastW(hero.Position);
                    }
                }

                if (Wtarget == null && Hp == 0)
                {
                    Wtarget = hero;
                    Hp = temp;
                }
                else if (temp < Hp)
                {
                    Wtarget = hero;
                    Hp = temp;
                }
            }

            if (Wtarget != null)
            {
                CastW(Wtarget.Position);
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
            var eprediction = Utils.Prediction.GetPrediction(predInput2);
            var Etarget = TargetSelector.GetTarget(E.Range, DamageType.Magical);

            if (pull && eprediction.Hitchance >= predE())
            {
                CastSpell(E, target, predE(), (int)E.Range);
            }
            else
            {
                var position = Player.ServerPosition - (eprediction.CastPosition - Player.ServerPosition);
                E.Cast(position);
            }

            if (Config.SafeLanternKey)
            {
                SafeLanternKeybind();
            }

            if (Config.UseSafeLantern)
            {
                SafeLantern();
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
                        Drawing.DrawCircle(Player.Position, Config.maxGrab, Color.Cyan);
                }
                else
                    Drawing.DrawCircle(Player.Position, Config.maxGrab, Color.Cyan);
            }

            if (Config.wRange)
            {
                if (Config.onlyRdy)
                {
                    if (E.IsReady())
                        Drawing.DrawCircle(Player.Position, W.Range, Color.Cyan);
                }
                else
                    Drawing.DrawCircle(Player.Position, W.Range, Color.Cyan);
            }

            if (Config.eRange)
            {
                if (Config.onlyRdy)
                {
                    if (E.IsReady())
                        Drawing.DrawCircle(Player.Position, E.Range, Color.Orange);
                }
                else
                    Drawing.DrawCircle(Player.Position, E.Range, Color.Orange);
            }

            if (Config.DrawTarget && target != null)
            {
                Drawing.DrawCircle(target.Position, 150, System.Drawing.Color.Red);
            }

            if (Config.rRange)
            {
                if (Config.onlyRdy)
                {
                    if (R.IsReady())
                        Drawing.DrawCircle(Player.Position, R.Range, Color.Gray);
                }
                else
                    Drawing.DrawCircle(Player.Position, R.Range, Color.Gray);
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
    }
}