﻿/*
 * Copyright 2020-2021 Ronald Ossendrijver. All rights reserved.
 */

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Svg;
using Svg.Pathing;
using Svg.Transforms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Treachery.Shared;
using System.Timers;

namespace Treachery.Test
{

    [TestClass]
    public class Tests
    {
        private List<Type> Written = new List<Type>();
        private void WriteSavegameIfApplicable(Game g, Type t)
        {
            if (!Written.Contains(t))
            {
                var playerWithAction = g.Players.FirstOrDefault(p => g.GetApplicableEvents(p, false).Contains(t));
                if (playerWithAction != null)
                {
                    lock (Written)
                    {
                        File.WriteAllText("" + (Written.Count + 100) + " " + t.Name + "-" + playerWithAction.Name.Replace('*', 'X') + ".special.json", GameState.GetStateAsString(g));
                        Written.Add(t);
                    }
                }
            }
        }

        private List<string> WrittenCases = new List<string>();
        private void WriteSavegameIfApplicable(Game g, Player playerWithAction, string c)
        {
            if (!WrittenCases.Contains(c))
            {
                lock (WrittenCases)
                {
                    var id = playerWithAction == null ? "x" : playerWithAction.Name.Replace('*', 'X');
                    File.WriteAllText(c + "-" + id + ".special.json", GameState.GetStateAsString(g));
                    WrittenCases.Add(c);
                }
            }
        }

        private ObjectCounter<int> _cardcount;
        private ObjectCounter<int> _leadercount;
        private string TestIllegalCases(Game g, GameEvent e)
        {
            var p = g.GetPlayer(e.Initiator);

            p = g.Players.FirstOrDefault(p => p.ForcesInReserve < 0 || p.SpecialForcesInReserve < 0);
            if (p != null) return "Negative forces: " + p + " after " + e.GetType().Name + " -> " + g.History.Count;

            p = g.Players.FirstOrDefault(p => p.Faction == Faction.White && (p.SpecialForcesInReserve != 0 || p.SpecialForcesKilled != 0));
            if (p != null) return "Invalid forces: " + p + " after " + e.GetType().Name + " -> " + g.History.Count;

            p = g.Players.FirstOrDefault(p => p.Resources < 0);
            if (p != null) return "Negative spice: " + p + " after " + e.GetType().Name + " -> " + g.History.Count;

            if (g.CurrentTurn >= 1)
            {
                p = g.Players.FirstOrDefault(p => 
                    p.ForcesInReserve + p.ForcesKilled + p.ForcesOnPlanet.Sum(b => b.Value.AmountOfForces) + 
                    (p.Faction != Faction.White ? p.SpecialForcesInReserve + p.SpecialForcesKilled + p.ForcesOnPlanet.Sum(b => b.Value.AmountOfSpecialForces) : 0) != 20);

                if (p != null)
                {
                    return "Illegal number of forces: " + p;
                }
            }

            if (g.Players.Any(p => p.Leaders.Count(l => g.IsInFrontOfShield(l)) > 1))
            {
                return "More than 1 leader in front of shield" + " after " + e.GetType().Name + " -> " + g.History.Count;
            }

            if (g.Players.Any(p => p.Leaders.Count(l => !g.CapturedLeaders.ContainsKey(l) && g.Skilled(l)) + g.CapturedLeaders.Count(cl => cl.Value == p.Faction && g.Skilled(cl.Key)) > 1))
            {
                return "More than 1 skilled leader for 1 player (not counting leaders captured by hark)" + " after " + e.GetType().Name + " -> " + g.History.Count;
            }

            if (e is SkillAssigned sa && sa.Leader == null)
            {
                return "Assigning skill to null leader" + " after " + e.GetType().Name + " -> " + g.History.Count;
            }

            if (g.SkillDeck != null)
            {
                var allCards = g.SkillDeck.Items.Concat(g.LeaderState.Where(ls => ls.Value.Skill != LeaderSkill.None).Select(ls => ls.Value.Skill)).ToArray();

                if (allCards.Any(item => allCards.Count(c => c == item) > 1))
                {
                    return "Duplicate card in Skill Deck" + " after " + e.GetType().Name + " -> " + g.History.Count;
                }
            }

            if (g.CurrentTurn >= 1)
            {
                int previousNumberOfCardsInPlay = _cardcount.CountOf(g.Seed);
                int currentNumberOfCards = 
                    g.Players.Sum(player => player.TreacheryCards.Count) 
                    + g.TreacheryDeck.Items.Count 
                    + g.TreacheryDiscardPile.Items.Count 
                    + (g.WhiteCache != null ? g.WhiteCache.Count : 0) 
                    + (g.CardsOnAuction != null ? g.CardsOnAuction.Items.Count : 0)
                    + (g.Players.Any(player => g.GetCardSetAsideForBid(player) != null) ? 1 : 0)
                    + g.RemovedTreacheryCards.Count;

                if (previousNumberOfCardsInPlay == 0)
                {
                    lock (_cardcount) {

                        _cardcount.SetToN(g.Seed, currentNumberOfCards);
                    }
                }
                else if (currentNumberOfCards != previousNumberOfCardsInPlay)
                {
                    return string.Format("Total number of cards has changed: {0} -> {1}.",
                        previousNumberOfCardsInPlay,
                        currentNumberOfCards);
                }
            }
            
            if (g.CurrentTurn >= 1)
            {
                int previousNumberOfLeadersInPlay = _leadercount.CountOf(g.Seed);
                int currentNumberOfLeaders = g.Players.Sum(player => player.Leaders.Count);
                if (previousNumberOfLeadersInPlay == 0)
                {
                    lock (_leadercount)
                    {
                        _leadercount.SetToN(g.Seed, currentNumberOfLeaders);
                    }
                }
                else if (currentNumberOfLeaders != previousNumberOfLeadersInPlay)
                {
                    return string.Format("Total number of leaders has changed: {0} -> {1}.",
                        previousNumberOfLeadersInPlay,
                        currentNumberOfLeaders);
                }
            }
            

            if (g.TreacheryDeck != null)
            {
                var allCards = g.TreacheryDeck.Items.Concat(g.TreacheryDiscardPile.Items).Concat(g.Players.SelectMany(p => p.TreacheryCards)).ToArray();
                if (allCards.Any(item => allCards.Count(c => c == item) > 1))
                {
                    return "Duplicate card in Treachery Card Deck" + " after " + e.GetType().Name + " -> " + g.History.Count;
                }
            }

            if (g.ResourceCardDeck != null)
            {
                var allCards = g.ResourceCardDeck.Items.Concat(g.ResourceCardDiscardPileA.Items).Concat(g.ResourceCardDiscardPileB.Items).ToArray();
                if (allCards.Any(item => allCards.Count(c => c == item) > 1))
                {
                    return "Duplicate card in Spice Deck" + " after " + e.GetType().Name + " -> " + g.History.Count;
                }
            }

            p = g.Players.FirstOrDefault(p => p.TreacheryCards.Count > p.MaximumNumberOfCards);
            if (p != null && g.CurrentPhase != Phase.PerformingKarmaHandSwap)
            {
                return "Too many cards: " + p + " after " + e.GetType().Name + " -> " + g.History.Count;
            }

            var blue = g.GetPlayer(Faction.Blue);
            if (blue != null &&
                blue.ForcesOnPlanet.Any(bat => bat.Value.AmountOfSpecialForces > 0 && !g.Players.Any(p => p.Occupies(bat.Key.Territory))))
            {
                return "Lonely advisor";
            }

            if (g.Players.Any(p => p.Leaders.Any(l => l.Faction != p.Faction && p.Faction != Faction.Purple && !g.CapturedLeaders.ContainsKey(l))))
            {
                return "Lost Leader";
            }

            return "";
        }

        private string TestSpecialCases(Game g, GameEvent e)
        {
            /*
            var p = e.Player;
            if (e is Shipment s && s.IsNoField && s.Initiator != Faction.White)
            {
                WriteSavegameIfApplicable(g, s.Player, "NoField shipment");
            }
            */
            return "";
            
        }

        private void ProfileGames()
        {
            Console.WriteLine("Profiling all savegame files in {0}...", Directory.GetCurrentDirectory());

            foreach (var f in Directory.EnumerateFiles(".", "savegame*.json"))
            {
                var testcaseFilename = f + ".testcase";
                if (!File.Exists(testcaseFilename))
                {
                    Console.WriteLine("Profiling {0}...", f);

                    var fs = File.OpenText(f);
                    var state = GameState.Load(fs.ReadToEnd());
                    var game = new Game(state.Version, false);
                    var testcase = new Testcase();

                    foreach (var e in state.Events)
                    {
                        e.Game = game;
                        e.Execute(true, true);
                        testcase.Testvalues.Add(DetermineTestvalues(game));
                    }

                    SaveObject(testcase, testcaseFilename);
                }
            }
        }

        [TestMethod]
        public void ImproveBots()
        {
            //Console.WriteLine("");

            //Expansion, advanced game:
            var rules = Game.RulesetDefinition[Ruleset.ExpansionAdvancedGame].ToList();
            rules.Add(Rule.FillWithBots);
            var allFactions = new List<Faction> { Faction.White, Faction.Brown, Faction.Grey, Faction.Green, Faction.Orange, Faction.Red, Faction.Blue, Faction.Yellow, Faction.Purple, Faction.Black };
            int nrOfPlayers = 6;
            int nrOfTurns = 7;
            rules.Add(Rule.BotsCannotAlly);
            var rulesAsArray = rules.ToArray();

            File.AppendAllLines("results.csv", new string[] { string.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8}",
                                                        "Battle_MimimumChanceToAssumeEnemyHeroSurvives",
                                                        "Battle_MimimumChanceToAssumeMyLeaderSurvives",
                                                        "Battle_MaxStrengthOfDialledForces",
                                                        "Battle_DialShortageThresholdForThrowing",
                                                        "Wins",
                                                        "Spice",
                                                        "Points",
                                                        "ForcesOnPlanet",
                                                        "Faction") });


            foreach (Faction toTest in allFactions) //10
            {
                for (float battle_MimimumChanceToAssumeEnemyHeroSurvives = 0.1f; battle_MimimumChanceToAssumeEnemyHeroSurvives <= 1; battle_MimimumChanceToAssumeEnemyHeroSurvives += 0.2f) // 5
                {
                    for (float battle_MimimumChanceToAssumeMyLeaderSurvives = 0.1f; battle_MimimumChanceToAssumeMyLeaderSurvives <= 1; battle_MimimumChanceToAssumeMyLeaderSurvives += 0.2f) //5
                    {
                        for (int Battle_MaxStrengthOfDialledForces = 8; Battle_MaxStrengthOfDialledForces <= 18; Battle_MaxStrengthOfDialledForces += 5) //3
                        {
                            for (int Battle_DialShortageThresholdForThrowing = 0; Battle_DialShortageThresholdForThrowing <= 6; Battle_DialShortageThresholdForThrowing += 3) //3
                            {
                                //10*5*5*3*3 = 2250 lines
                                var p = BotParameters.GetDefaultParameters(toTest);
                                p.Battle_MimimumChanceToAssumeEnemyHeroSurvives = battle_MimimumChanceToAssumeEnemyHeroSurvives;
                                p.Battle_MimimumChanceToAssumeMyLeaderSurvives = battle_MimimumChanceToAssumeMyLeaderSurvives;
                                p.Battle_MaxStrengthOfDialledForces = Battle_MaxStrengthOfDialledForces;
                                p.Battle_DialShortageThresholdForThrowing = Battle_DialShortageThresholdForThrowing;

                                var pDict = new Dictionary<Faction, BotParameters>() { { toTest, p } };

                                var factions = allFactions.Where(f => f != toTest).TakeRandomN(nrOfPlayers - 1).ToList();
                                factions.Add(toTest);
                                DetermineWins(Environment.ProcessorCount * 3, rulesAsArray, factions, nrOfPlayers, nrOfTurns, pDict, toTest, out int wins, out int spice, out int points, out int forcesOnPlanet);

                                File.AppendAllLines("results.csv", new string[] { string.Format("{0};{1};{2};{3};{4};{5};{6};{7};{8}",
                                                        battle_MimimumChanceToAssumeEnemyHeroSurvives, battle_MimimumChanceToAssumeMyLeaderSurvives, Battle_MaxStrengthOfDialledForces, Battle_DialShortageThresholdForThrowing,
                                                        wins, spice, points, forcesOnPlanet, toTest) });
                            }
                        }
                    }
                }
            }
        }

        private void DetermineWins(int nrOfGames, Rule[] rules, List<Faction> factions, int nrOfPlayers, int nrOfTurns, Dictionary<Faction, BotParameters> p, Faction f,
            out int wins, out int spice, out int points, out int forcesOnPlanet)
        {
            int countWins = 0;
            int countSpice = 0;
            int countPoints = 0;
            int countForcesOnPlanet = 0;

            var po = new ParallelOptions();
            po.MaxDegreeOfParallelism = Environment.ProcessorCount;
            Parallel.For(0, nrOfGames, po,
                i =>
                {
                    var game = LetBotsPlay(rules, factions, nrOfPlayers, nrOfTurns, p, false, false);
                    var playerToCheck = game.Players.Single(p => p.Faction == f);
                    if (game.Winners.Contains(playerToCheck)) countWins++;
                    countSpice += playerToCheck.Resources;
                    countPoints += game.NumberOfVictoryPoints(playerToCheck, true);
                    countForcesOnPlanet += playerToCheck.ForcesOnPlanet.Sum(kvp => kvp.Value.TotalAmountOfForces);
                });

            wins = countWins;
            spice = countSpice;
            points = countPoints;
            forcesOnPlanet = countForcesOnPlanet;
        }

        [TestMethod]
        public void TestBots()
        {
            _cardcount = new();
            _leadercount = new();

            int nrOfGames = 100;

            Console.WriteLine("Winner;Method;Turn;Events;Leaders killed;Forces killed;Owned cards;Owned Spice;Discarded");

            //Expansion, advanced game, all expansions, all factions:
            var rules = Game.RulesetDefinition[Ruleset.ExpansionAdvancedGame].ToList();
            rules.Add(Rule.FillWithBots);
            rules.Add(Rule.AssistedNotekeeping);
            var factions = EstablishPlayers.AvailableFactions().ToList();
            int nrOfTurns = 7;
            int nrOfPlayers = 6;


            //Expansion, advanced game, all expansions, free for all without guild and fremen:
            /*
            var rules = Game.RulesetDefinition[Ruleset.AllExpansionsAdvancedGame].ToList();
            rules.Add(Rule.FillWithBots);
            rules.Add(Rule.BotsCannotAlly);
            var factions = EstablishPlayers.AvailableFactions().Except(new Faction[] { Faction.Orange, Faction.Yellow, Faction.Red, Faction.Purple }).ToList();
            int nrOfTurns = 7; 
            int nrOfPlayers = factions.Count;
            */

            /*
            var rules = Game.RulesetDefinition[Ruleset.AllExpansionsBasicGame].ToList();
            rules.Add(Rule.FillWithBots);
            rules.Add(Rule.BotsCannotAlly);
            var factions = EstablishPlayers.AvailableFactions().Except(new Faction[] { Faction.Orange, Faction.Yellow }).ToList();
            int nrOfTurns = 7;
            int nrOfPlayers = factions.Count;
            */

            //Expansion, advanced game, all expansions, free for all without guild and fremen:
            //var rules = Game.RulesetDefinition[Ruleset.AllExpansionsAdvancedGame].ToList();
            //rules.Add(Rule.FillWithBots);
            //rules.Add(Rule.BotsCannotAlly);
            //var factions = EstablishPlayers.AvailableFactions().Except(new Faction[] { Faction.Orange, Faction.Yellow }).ToList();
            //int nrOfTurns = 7; 
            //int nrOfPlayers = factions.Count;

            //Expansion, advanced game, 8 players:
            //var rules = Game.RulesetDefinition[Ruleset.ExpansionAdvancedGame].ToList();
            //rules.Add(Rule.FillWithBots);
            //rules.Add(Rule.ExtraKaramaCards);
            //rules.Add(Rule.AssistedNotekeeping);
            //var factions = EstablishPlayers.AvailableFactions().ToList();
            //int nrOfPlayers = factions.Count;

            //Game to find a specific situation to test
            //var rules = Game.RulesetDefinition[Ruleset.ExpansionAdvancedGame].ToList();
            //rules.Add(Rule.FillWithBots);
            //rules.Add(Rule.ExtraKaramaCards);
            //rules.Add(Rule.AssistedNotekeeping);
            //var factions = new List<Faction>() { Faction.Black, Faction.Green, Faction.Red, Faction.Brown };
            //int nrOfPlayers = factions.Count;

            //Expansion, advanced game, 6 players:
            //var rules = Game.RulesetDefinition[Ruleset.ExpansionAdvancedGame].ToList();
            //rules.Add(Rule.FillWithBots);
            //rules.Add(Rule.BotsCannotAlly);
            //var factions = new List<Faction>() { Faction.Black, Faction.Green, Faction.Yellow, Faction.Red, Faction.Grey, Faction.Blue };
            //int nrOfPlayers = factions.Count;

            //Expansion, advanced game, 8 players:
            //var rules = Game.RulesetDefinition[Ruleset.ExpansionAdvancedGame].ToList();
            //rules.Add(Rule.FillWithBots);
            //var factions = new List<Faction>() { Faction.Black, Faction.Green, Faction.Grey, Faction.Red, Faction.Purple, Faction.Blue };
            //int nrOfPlayers = factions.Count;

            //Classic, basic game:
            //var rules = Game.RulesetDefinition[Ruleset.ServerClassic].ToList();
            //rules.Add(Rule.FillWithBots);
            //var factions = new List<Faction>() { Faction.Black, Faction.Green, Faction.Yellow, Faction.Red, Faction.Orange, Faction.Blue };
            //int nrOfPlayers = factions.Count;

            //Server Classic, advanced game:
            //var rules = Game.RulesetDefinition[Ruleset.ServerClassic].ToList();
            //rules.Add(Rule.FillWithBots);
            //var factions = new List<Faction>() { Faction.Black, Faction.Green, Faction.Yellow, Faction.Red, Faction.Orange, Faction.Blue };
            //int nrOfPlayers = factions.Count;

            //All rules enables:
            //var rules = Enumerations.GetValuesExceptDefault(typeof(Rule), Rule.None).ToList();
            //var factions = Enumerations.GetValuesExceptDefault(typeof(Faction), Faction.None).ToList();
            //rules.Remove(Rule.CustomInitialForcesAndResources);
            //rules.Remove(rules.Add(Rule.BotsCannotAlly));
            //int nrOfPlayers = factions.Count;

            /*rules.Add(Rule.RedBot);
            rules.Add(Rule.OrangeBot);
            rules.Add(Rule.BlackBot);
            rules.Add(Rule.PurpleBot);
            rules.Add(Rule.BlueBot);
            rules.Add(Rule.GreenBot);
            rules.Add(Rule.YellowBot);
            rules.Add(Rule.GreyBot);*/

            //Can bots ally?
            //rules.Add(Rule.BotsCannotAlly);

            var rulesAsArray = rules.ToArray();
            var wincounter = new ObjectCounter<Faction>();

            ParallelOptions po = new ParallelOptions();
            po.MaxDegreeOfParallelism = Environment.ProcessorCount;
            Parallel.For(0, nrOfGames, po,
                   index =>
                   {
                       PlayGameAndRecordResults(factions, nrOfPlayers, nrOfTurns, rulesAsArray, wincounter);
                   });

            foreach (var f in wincounter.Counted)
            {
                Console.WriteLine("{0}: {1} ({2}%)", f, wincounter.CountOf(f), (100f * wincounter.CountOf(f) / nrOfGames));
            }

        }

        private void PlayGameAndRecordResults(List<Faction> factions, int nrOfPlayers, int nrOfTurns, Rule[] rulesAsArray, ObjectCounter<Faction> wincounter)
        {
            var game = LetBotsPlay(rulesAsArray, factions, nrOfPlayers, nrOfTurns, null, false, true);

            Console.WriteLine("{0};{1};{2};{3};{4};{5};{6};{7};{8}",
                string.Join(",", game.Winners),
                Skin.Current.Describe(game.WinMethod),
                game.CurrentTurn,
                game.History.Count,
                string.Join(",", game.LeaderState.Where(l => !l.Value.Alive).Select(l => l.Key)),
                string.Join(",", game.Players.Sum(p => p.ForcesKilled + p.SpecialForcesKilled)),
                string.Join(",", game.Players.SelectMany(p => p.TreacheryCards)),
                string.Join(",", game.Players.Sum(p => p.Resources)),
                string.Join(",", game.TreacheryDiscardPile.Items));

            foreach (var winner in game.Winners)
            {
                wincounter.Count(winner.Faction);
            }
        }

        private List<TimedTest> timedTests = new List<TimedTest>();
        private List<Game> failedGames = new List<Game>();
        private Game LetBotsPlay(Rule[] rules, List<Faction> factions, int nrOfPlayers, int nrOfTurns, Dictionary<Faction, BotParameters> p, bool infoLogging, bool performTests)
        {
            var game = new Game(false)
            {
                BotInfologging = infoLogging,
            };

            var timer = new TimedTest(game, 30);
            timer.Elapsed += HandleElapsedTestTime;
            timedTests.Add(timer);

            try
            {
                var start = new EstablishPlayers(game) { ApplicableRules = rules.ToArray(), FactionsInPlay = factions, MaximumTurns = nrOfTurns, MaximumNumberOfPlayers = nrOfPlayers, Players = Array.Empty<string>(), Seed = new Random().Next() };
                start.Time = DateTime.Now;
                start.Execute(false, true);
                
                if (p != null)
                {
                    foreach (var kvp in p)
                    {
                        game.Players.Single(p => p.Faction == kvp.Key).Param = kvp.Value;
                    }
                }

                while (game.CurrentPhase != Phase.GameEnded)
                {
                    var evt = PerformBotEvent(game, performTests);

                    if (performTests)
                    {
                        if (evt == null)
                        {
                            File.WriteAllText("novalidbotevent" + game.Seed + ".json", GameState.GetStateAsString(game));
                        }
                        Assert.IsNotNull(evt, "bots couldn't come up with a valid event");

                        evt.Time = DateTime.Now;

                        var illegalCase = TestIllegalCases(game, evt);
                        if (illegalCase != "")
                        {
                            File.WriteAllText("illegalcase" + game.Seed + ".json", GameState.GetStateAsString(game));
                        }
                        Assert.AreEqual("", illegalCase);

                        var strangeCase = TestSpecialCases(game, evt);
                        if (strangeCase != "")
                        {
                            File.WriteAllText("strangecase" + game.Seed + ".json", GameState.GetStateAsString(game));
                        }
                        Assert.AreEqual("", strangeCase);

                        if (game.History.Count == 5000)
                        {
                            File.WriteAllText("stuck" + game.Seed + ".json", GameState.GetStateAsString(game));
                        }
                        Assert.AreNotEqual(5000, game.History.Count, "bots got stuck at 5000 events");

                        if (failedGames.Contains(game))
                        {
                            File.WriteAllText("timeout" + game.Seed + ".json", GameState.GetStateAsString(game));
                            failedGames.Remove(game);
                        }
                        Assert.IsFalse(failedGames.Contains(game), "timeout");
                    }
                    else if (game.History.Count == 5000)
                    {
                        File.WriteAllText("stuck" + game.Seed + ".json", GameState.GetStateAsString(game));
                        break;
                    }
                    else if (failedGames.Contains(game))
                    {
                        File.WriteAllText("timeout" + game.Seed + ".json", GameState.GetStateAsString(game));
                        failedGames.Remove(game);
                        break;
                    }
                }
            }
            catch
            {
                timer.Stop();
                timedTests.Remove(timer);
                throw;
            }

            timer.Stop();
            timedTests.Remove(timer);

            return game;
        }

        private void HandleElapsedTestTime(object sender, ElapsedEventArgs e)
        {
            var game = sender as Game;
            failedGames.Add(game);
        }

        private GameEvent PerformBotEvent(Game game, bool performTests)
        {
            var bots = Deck<Player>.Randomize(game.Players.Where(p => p.IsBot));

            foreach (var bot in bots)
            {
                var evt = bot.DetermineHighPrioInPhaseAction(game.GetApplicableEvents(bot, true));

                if (evt != null)
                {
                    var executeResult = evt.Execute(performTests, true);
                    if (performTests && executeResult != "")
                    {
                        File.WriteAllText("invalid" + game.Seed + ".json", GameState.GetStateAsString(game));
                    }
                    if (performTests) Assert.AreEqual("", executeResult);
                    return evt;
                }
            }

            foreach (var bot in bots)
            {
                var evt = bot.DetermineMiddlePrioInPhaseAction(game.GetApplicableEvents(bot, true));

                if (evt != null)
                {
                    var executeResult = evt.Execute(performTests, true);
                    if (performTests && executeResult != "")
                    {
                        File.WriteAllText("invalid" + game.Seed + ".json", GameState.GetStateAsString(game));
                    }
                    if (performTests) Assert.AreEqual("", executeResult);
                    return evt;
                }
            }

            foreach (var bot in bots)
            {
                var evt = bot.DetermineLowPrioInPhaseAction(game.GetApplicableEvents(bot, true));

                if (evt != null)
                {
                    var executeResult = evt.Execute(performTests, true);
                    if (performTests && executeResult != "")
                    {
                        File.WriteAllText("invalid" + game.Seed + ".json", GameState.GetStateAsString(game));
                    }
                    if (performTests) Assert.AreEqual("", executeResult);
                    return evt;
                }
            }

            foreach (var p in game.Players.Where(p => p.IsBot))
            {
                var evt = p.DetermineEndPhaseAction(game.GetApplicableEvents(p, true));

                if (evt != null)
                {
                    var result = evt.Execute(performTests, true);
                    if (performTests)
                    {
                        if (result != "") File.WriteAllText("error" + game.Seed + ".json", GameState.GetStateAsString(game));
                        Assert.AreEqual("", result);
                    }
                    return evt;
                }
            }

            return null;
        }

        [TestMethod]
        public void Regression()
        {
            ProfileGames();

            _cardcount = new();
            _leadercount = new();

            try
            {
                Console.WriteLine("Re-playing all savegame files in {0}...", Directory.GetCurrentDirectory());

                int gamesTested = 0;
                ParallelOptions po = new ParallelOptions();
                po.MaxDegreeOfParallelism = Environment.ProcessorCount;
                Parallel.ForEach(Directory.EnumerateFiles(".", "savegame*.json"), po, f =>
                {
                    gamesTested++;
                    var fs = File.OpenText(f);
                    var state = GameState.Load(fs.ReadToEnd());
                    Console.WriteLine("Checking {0} (version {1})...", f, state.Version);
                    var game = new Game(state.Version, false);

                    fs = File.OpenText(f + ".testcase");
                    var tc = LoadObject<Testcase>(fs.ReadToEnd());

                    int valueId = 0;
                    foreach (var e in state.Events)
                    {
                        e.Game = game;
                        var previousPhase = game.CurrentPhase;

                        var result = e.Execute(true, true);
                        if (result != "")
                        {
                            File.WriteAllText("invalid.json", GameState.GetStateAsString(game));
                        }
                        Assert.AreEqual("", result, f + ", " + e.GetType().Name + " (" + valueId + ", " + e.GetMessage() + ")");

                        var actualValues = DetermineTestvalues(game);
                        tc.Testvalues[valueId].Equals(actualValues);
                        if (!tc.Testvalues[valueId].Equals(actualValues))
                        {
                            File.WriteAllText("invalid.json", GameState.GetStateAsString(game));
                        }
                        Assert.AreEqual(tc.Testvalues[valueId], actualValues, f + ", " + previousPhase + " -> " + game.CurrentPhase + ", " + e.GetType().Name + " (" + valueId + ", " + e.GetMessage() + "): " + Testvalues.Difference);

                        var strangeCase = TestIllegalCases(game, e);
                        if (strangeCase != "")
                        {
                            File.WriteAllText("illegalcase.json", GameState.GetStateAsString(game));
                        }
                        Assert.AreEqual("", strangeCase, f + ", " + strangeCase);

                        valueId++;
                    }
                });

                Assert.AreNotEqual(0, gamesTested);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }

        /*
        [TestMethod]
        public void Statistics()
        {
            try
            {
                var wonbattles = new ObjectCounter<string>();
                var lostbattles = new ObjectCounter<string>();
                var leaderInBattle = new ObjectCounter<string>();
                var leaderCalledAsTraitor = new ObjectCounter<string>();

                int gamesTested = 0;
                ParallelOptions po = new ParallelOptions();
                po.MaxDegreeOfParallelism = Environment.ProcessorCount;
                Parallel.ForEach(Directory.EnumerateFiles(".", "savegame*.json"), po, f =>
                {
                    gamesTested++;
                    var fs = File.OpenText(f);
                    var state = GameState.Load(fs.ReadToEnd());
                    var game = new Game(state.Version);

                    fs = File.OpenText(f + ".testcase");
                    var tc = LoadObject<Testcase>(fs.ReadToEnd());

                    foreach (var e in state.Events)
                    {
                        e.Game = game;
                        var result = e.Execute(true, true);

                        if (e is BattleConcluded bc)
                        {
                            var winner = game.CurrentBattle.PlanOf(bc.Initiator).Hero;
                            var loser = game.CurrentBattle.PlanOfOpponent(bc.Player).Hero;
                            wonbattles.Count("" + winner);
                            lostbattles.Count("" + loser);
                        }

                        if (e is TreacheryCalled trc && trc.TraitorCalled)
                        {
                            var traitor = game.CurrentBattle.PlanOfOpponent(trc.Player).Hero;
                            leaderCalledAsTraitor.Count("" + traitor);
                        }

                    }
                });

                //Statistics

                Console.WriteLine("Leader;Won Battles;Lost Battles;Called as traitor");
                foreach (var c in lostbattles.Counted)
                {
                    Console.WriteLine("{0};{1};{2};{3}", c, wonbattles.CountOf(c), lostbattles.CountOf(c), leaderCalledAsTraitor.CountOf(c));
                }

                //End Statistics
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                throw;
            }
        }
        */

        private Testvalues DetermineTestvalues(Game game)
        {
            var result = new Testvalues
            {
                currentPhase = game.CurrentPhase,
                forcesinArrakeen = game.ForcesOnPlanet[game.Map.Arrakeen].Sum(b => b.TotalAmountOfForces),
                forcesinCarthag = game.ForcesOnPlanet[game.Map.Carthag].Sum(b => b.TotalAmountOfForces),
                forcesinTabr = game.ForcesOnPlanet[game.Map.SietchTabr].Sum(b => b.TotalAmountOfForces),
                forcesinHabbanya = game.ForcesOnPlanet[game.Map.HabbanyaSietch].Sum(b => b.TotalAmountOfForces),
                forcesinTuek = game.ForcesOnPlanet[game.Map.TueksSietch].Sum(b => b.TotalAmountOfForces),
                nrofplayers = game.Players.Count,
                playervalues = new TestvaluesPerPlayer[game.Players.Count]
            };

            for (int i = 0; i < game.Players.Count; i++)
            {
                var p = game.Players[i];

                result.playervalues[i] = new TestvaluesPerPlayer()
                {
                    faction = p.Faction,
                    ally = p.Ally,
                    position = p.PositionAtTable,
                    resources = p.Resources,
                    bribes = p.Bribes,
                    forcesinreserve = p.ForcesInReserve,
                    specialforcesinreserve = p.SpecialForcesInReserve,
                    totaldeathcount = p.Leaders.Sum(l => game.LeaderState[l].DeathCounter),
                    cardcount = p.TreacheryCards.Count,
                    cardtypes = p.TreacheryCards.Sum(c => (int)c.Type),
                    traitors = p.Traitors.Sum(t => t.Id),
                    facedancers = p.FaceDancers.Sum(t => t.Id),
                    totalforcesonplanet = p.ForcesOnPlanet.Values.Sum(b => b.AmountOfForces),
                    totalspecialforcesonplanet = p.ForcesOnPlanet.Values.Sum(b => b.AmountOfSpecialForces),
                    forceskilled = p.ForcesKilled,
                    specialforceskilled = p.SpecialForcesKilled,
                    nroftechtokens = p.TechTokens.Count
                };
            }

            return result;
        }

        private static T LoadObject<T>(string data)
        {
            var serializer = JsonSerializer.CreateDefault();
            serializer.TypeNameHandling = TypeNameHandling.All;
            var textReader = new StringReader(data);
            var jsonReader = new JsonTextReader(textReader);
            return serializer.Deserialize<T>(jsonReader);
        }


        [TestMethod]
        public void SaveAndLoadSkin()
        {
            var leader = LeaderManager.LeaderLookup.Find(1008);
            var oldName = Skin.Current.GetPersonName(leader);
            var serializer = JsonSerializer.CreateDefault();
            serializer.Formatting = Formatting.Indented;
            var writer = new StringWriter();
            serializer.Serialize(writer, Skin.Dune1979);
            writer.Close();
            var skinData = writer.ToString();
            File.WriteAllText("skin.json", skinData);

            var textReader = new StringReader(File.ReadAllText("skin.json"));
            var jsonReader = new JsonTextReader(textReader);
            var skinToTest = serializer.Deserialize<Skin>(jsonReader);
            Assert.AreEqual(oldName, skinToTest.GetPersonName(leader));
        }

        //[TestMethod]
        public static void SaveBuiltInSkins()
        {
            SaveObject(Skin.Dune1979, "e:\\Skin.Dune.treachery.online.json");
        }

        [TestMethod]
        public static void SVG()
        {
            var accumulatedTransforms = new List<SvgTransformCollection>();

            var doc = SvgDocument.Open("c:\\map.svg");
            foreach (var group1 in doc.Children)
            {
                if (group1.ID == "Dune")
                {
                    if (group1.Transforms != null)
                    {
                        accumulatedTransforms.Add(group1.Transforms);
                    }

                    foreach (var child in group1.Children)
                    {
                        if (child.ID == "Areas")
                        {
                            if (child.Transforms != null)
                            {
                                accumulatedTransforms.Add(child.Transforms);
                            }

                            foreach (var areaOrSubgroup in child.Children)
                            {
                                Console.WriteLine(areaOrSubgroup.GetType());

                                if (areaOrSubgroup is SvgGroup)
                                {
                                    if (areaOrSubgroup.Transforms != null)
                                    {
                                        accumulatedTransforms.Add(areaOrSubgroup.Transforms);
                                    }

                                    foreach (var area in areaOrSubgroup.Children)
                                    {
                                        if (area is SvgPath)
                                        {
                                            ProcessPath(accumulatedTransforms, area);
                                        }
                                    }
                                }
                                else if (areaOrSubgroup is SvgPath)
                                {
                                    ProcessPath(accumulatedTransforms, areaOrSubgroup);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static void ProcessPath(List<SvgTransformCollection> accumulatedTransforms, SvgElement area)
        {
            var path = area as SvgPath;
            //Console.WriteLine(area.ID);

            var shape = new List<Segment>();

            foreach (var elt in path.PathData)
            {
                if (elt is SvgClosePathSegment)
                {
                    shape.Add(new Close());
                }
                else if (elt is SvgCubicCurveSegment)
                {
                    var x = elt as SvgCubicCurveSegment;
                    shape.Add(new BezierTo(Transform(x.Start, accumulatedTransforms), Transform(x.End, accumulatedTransforms), Transform(x.FirstControlPoint, accumulatedTransforms), Transform(x.SecondControlPoint, accumulatedTransforms)));
                }
                else if (elt is SvgLineSegment)
                {
                    var x = elt as SvgLineSegment;
                    shape.Add(new LineTo(Transform(x.Start, accumulatedTransforms), Transform(x.End, accumulatedTransforms)));
                }
                else if (elt is SvgMoveToSegment)
                {
                    var x = elt as SvgMoveToSegment;
                    shape.Add(new MoveTo(Transform(x.Start, accumulatedTransforms), Transform(x.End, accumulatedTransforms)));
                }
            }

            Console.WriteLine("private static readonly Segment[] {1} = {{ {0} }};", string.Join(", ", shape), area.ID);

            var xml = area.GetXML();
            int end = xml.IndexOf("z\"") + 1;
            var newPath = string.Join(' ', shape.Select(s => s.ToSvgString()));
            //Console.WriteLine( "{0}{1}{2}", xml.Substring(0, 9), newPath, xml.Substring(end, xml.Length - end));
        }

        public static void TranslatePoints()
        {
            Game g = new Game();
            foreach (var l in g.Map.Locations)
            {
                Console.WriteLine("{0};{1};{2};{3};{4};{5};{6}",
                    l.Territory.Id,
                    l.Sector,
                    l.Name,
                    (int)(l.Center.X / 7.32),
                    (int)(l.Center.Y / 7.32),
                    l.SpiceLocation != null ? (int)(l.SpiceLocation.X / 7.32) : -1,
                    l.SpiceLocation != null ? (int)(l.SpiceLocation.Y / 7.32) : -1);
            }
        }

        private static PointF Transform(PointF p, IEnumerable<SvgTransformCollection> transforms)
        {
            var thePoints = new PointF[] { p };

            foreach (var tc in transforms)
            {
                foreach (var t in tc)
                {
                    t.Matrix.TransformPoints(thePoints);
                }
            }

            return new PointF((int)(7.362344583f * thePoints[0].X), (int)(7.349840256f * thePoints[0].Y));
        }

        private static void SaveObject(object toSave, string filename)
        {
            var serializer = JsonSerializer.CreateDefault();
            serializer.Formatting = Formatting.Indented;
            var writer = new StringWriter();
            serializer.Serialize(writer, toSave);
            writer.Close();
            var skinData = writer.ToString();
            File.WriteAllText(filename, skinData);
        }

        private static int GetRandomId()
        {
            return (new Random()).Next();
        }

        [TestMethod]
        public void TestShuffleMethod()
        {
            int[] counters = new int[100];

            for (int run = 0; run < 100000; run++)
            {
                var deck = new Deck<int>(Enumerable.Range(0, 100), new Random());
                deck.Shuffle();
                counters[deck.Draw()]++;
            }

            Console.WriteLine("Average times any item is on top: {0}", counters.Average());
            Console.WriteLine("Standard deviation in the times any item is on top: {0}", CalculateStandardDeviation(counters.Select(c => (double)c)));

            for (int i = 0; i < 100; i++)
            {
                Console.WriteLine("Times {0} was on top: {1}", i, counters[i]);
            }
        }

        private double CalculateStandardDeviation(IEnumerable<double> values)
        {
            double standardDeviation = 0;

            if (values.Any())
            {
                // Compute the average.     
                double avg = values.Average();

                // Perform the Sum of (value-avg)_2_2.      
                double sum = values.Sum(d => Math.Pow(d - avg, 2));

                // Put it all together.      
                standardDeviation = Math.Sqrt((sum) / (values.Count() - 1));
            }

            return standardDeviation;
        }
                
        [TestMethod]
        public void DetermineBias()
        {
            var joined = new List<string>()
            {
                "Ronald",
                "Rene",
                "Ren�",
                "X1",
                "X2",
                ""
            };

            var factions = new List<Faction> { Faction.Green, Faction.Black, Faction.Yellow, Faction.Red, Faction.Orange, Faction.Blue };

            var assignedFactions = new List<Tuple<string, Faction>>();
            var assignedPositions = new List<Tuple<Faction, int>>();
            for (int i = 0; i < 10000; i++)
            {
                int _playerID = GetRandomId();
                var game = new Game(_playerID, false);

                game.HandleEvent(new EstablishPlayers() { Players = joined, FactionsInPlay = factions, MaximumTurns = 10, ApplicableRules = Game.RulesetDefinition[Ruleset.AdvancedGame], Seed = _playerID });

                foreach (var p in game.Players)
                {
                    assignedFactions.Add(new Tuple<string, Faction>(p.Name, p.Faction));
                    assignedPositions.Add(new Tuple<Faction, int>(p.Faction, p.PositionAtTable));
                }
            }

            Console.WriteLine("{0}: {1} {2} {3} {4} {5} {6}", "Player".PadLeft(20), "Atr".PadLeft(5), "Hrk".PadLeft(5), "Fre".PadLeft(5), "Emp".PadLeft(5), "Gld".PadLeft(5), "Bgt".PadLeft(5));
            foreach (var playerName in joined)
            {
                var a = assignedFactions.Count(t => t.Item1 == playerName && t.Item2 == Faction.Green).ToString();
                var h = assignedFactions.Count(t => t.Item1 == playerName && t.Item2 == Faction.Black).ToString();
                var f = assignedFactions.Count(t => t.Item1 == playerName && t.Item2 == Faction.Yellow).ToString();
                var e = assignedFactions.Count(t => t.Item1 == playerName && t.Item2 == Faction.Red).ToString();
                var g = assignedFactions.Count(t => t.Item1 == playerName && t.Item2 == Faction.Orange).ToString();
                var b = assignedFactions.Count(t => t.Item1 == playerName && t.Item2 == Faction.Blue).ToString();
                Console.WriteLine("{0}: {1} {2} {3} {4} {5} {6}", playerName.PadLeft(20), a.PadLeft(5), h.PadLeft(5), f.PadLeft(5), e.PadLeft(5), g.PadLeft(5), b.PadLeft(5));
            }

            Console.WriteLine("{0}: {1} {2} {3} {4} {5} {6}", "Faction".PadLeft(20), "0".PadLeft(5), "1".PadLeft(5), "2".PadLeft(5), "3".PadLeft(5), "4".PadLeft(5), "5".PadLeft(5));
            foreach (var faction in factions)
            {
                var p0 = assignedPositions.Count(t => t.Item1 == faction && t.Item2 == 0).ToString();
                var p1 = assignedPositions.Count(t => t.Item1 == faction && t.Item2 == 1).ToString();
                var p2 = assignedPositions.Count(t => t.Item1 == faction && t.Item2 == 2).ToString();
                var p3 = assignedPositions.Count(t => t.Item1 == faction && t.Item2 == 3).ToString();
                var p4 = assignedPositions.Count(t => t.Item1 == faction && t.Item2 == 4).ToString();
                var p5 = assignedPositions.Count(t => t.Item1 == faction && t.Item2 == 5).ToString();
                Console.WriteLine("{0}: {1} {2} {3} {4} {5} {6}", faction.ToString().PadLeft(20), p0.PadLeft(5), p1.PadLeft(5), p2.PadLeft(5), p3.PadLeft(5), p4.PadLeft(5), p5.PadLeft(5));
            }
        }
    }
}
