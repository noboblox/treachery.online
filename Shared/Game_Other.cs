﻿/*
 * Copyright 2020-2022 Ronald Ossendrijver. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Treachery.Shared
{
    public partial class Game
    {
        public void HandleEvent(WhiteRevealedNoField e)
        {
            RevealCurrentNoField(e.Player);
        }

        private void RevealCurrentNoField(Player player, Location inLocation = null)
        {
            if (player != null && player.Faction == Faction.White)
            {
                var noFieldLocation = player.ForcesOnPlanet.FirstOrDefault(kvp => kvp.Value.AmountOfSpecialForces > 0).Key;

                if (noFieldLocation != null)
                {
                    if (inLocation == null || inLocation == noFieldLocation)
                    {
                        LatestRevealedNoFieldValue = CurrentNoFieldValue;
                        player.SpecialForcesToReserves(noFieldLocation, 1);
                        int nrOfForces = Math.Min(player.ForcesInReserve, CurrentNoFieldValue);
                        player.ShipForces(noFieldLocation, nrOfForces);
                        CurrentReport.Express(player.Faction, " reveal ", nrOfForces, FactionForce.White, " under ", FactionSpecialForce.White, CurrentNoFieldValue, " in ", noFieldLocation);

                        if (CurrentNoFieldValue == 0)
                        {
                            FlipBeneGesseritWhenAlone();
                        }

                        CurrentNoFieldValue = -1;
                    }
                }
            }
        }

        private void RevealCurrentNoField(Player player, Territory inTerritory)
        {
            if (player != null && player.Faction == Faction.White)
            {
                foreach (var l in inTerritory.Locations)
                {
                    RevealCurrentNoField(player, l);
                }
            }
        }

        public void HandleEvent(BlueBattleAnnouncement e)
        {
            var initiator = GetPlayer(e.Initiator);
            initiator.FlipForces(e.Territory, false);
            CurrentReport.Express(e);
        }

        public void HandleEvent(Donated e)
        {
            var target = GetPlayer(e.Target);

            if (!e.FromBank)
            {
                var initiator = GetPlayer(e.Initiator);

                ExchangeResourcesInBribe(initiator, target, e.Resources);

                if (e.Card != null)
                {
                    initiator.TreacheryCards.Remove(e.Card);
                    RegisterKnown(initiator, e.Card);
                    target.TreacheryCards.Add(e.Card);

                    foreach (var p in Players.Where(p => p != initiator && p != target))
                    {
                        UnregisterKnown(p, initiator.TreacheryCards);
                        UnregisterKnown(p, target.TreacheryCards);
                    }
                }

                CurrentReport.Express(e);
                RecentMilestones.Add(Milestone.Bribe);
            }
            else
            {
                if (e.Resources < 0)
                {
                    int resourcesToTake = Math.Min(Math.Abs(e.Resources), target.Resources);
                    CurrentReport.Express("Host puts ", Payment(resourcesToTake), " from ", e.Target, " into the ", Concept.Resource, " Bank");
                    target.Resources -= resourcesToTake;
                }
                else
                {
                    CurrentReport.Express("Host gives ", e.Target, Payment(e.Resources), " from the ", Concept.Resource, " Bank");
                    target.Resources += e.Resources;
                }
            }
        }

        public void HandleEvent(DistransUsed e)
        {
            var initiator = GetPlayer(e.Initiator);
            var target = GetPlayer(e.Target);

            bool targetHadRoomForCards = target.HasRoomForCards;

            Discard(initiator, TreacheryCardType.Distrans);

            initiator.TreacheryCards.Remove(e.Card);
            RegisterKnown(initiator, e.Card);
            target.TreacheryCards.Add(e.Card);

            if (initiator.TreacheryCards.Any())
            {
                foreach (var p in Players.Where(p => p != initiator && p != target))
                {
                    UnregisterKnown(p, initiator.TreacheryCards);
                    UnregisterKnown(p, target.TreacheryCards);
                }
            }

            CurrentReport.Express(e);

            CheckIfBiddingForPlayerShouldBeSkipped(target, targetHadRoomForCards);
        }

        private void CheckIfBiddingForPlayerShouldBeSkipped(Player player, bool hadRoomForCards)
        {
            if (CurrentPhase == Phase.BlackMarketBidding && hadRoomForCards && !player.HasRoomForCards && BlackMarketBid.MayBePlayed(this, player))
            {
                HandleEvent(new Bid(this) { Initiator = player.Faction, Passed = true });
            }
            else if (CurrentPhase == Phase.Bidding && hadRoomForCards && !player.HasRoomForCards && Bid.MayBePlayed(this, player))
            {
                HandleEvent(new Bid(this) { Initiator = player.Faction, Passed = true });
            }
        }

        public void HandleEvent(DiscardedTaken e)
        {
            CurrentReport.Express(e);
            RecentlyDiscarded.Remove(e.Card);
            TreacheryDiscardPile.Items.Remove(e.Card);
            e.Player.TreacheryCards.Add(e.Card);
            Discard(e.Player, TreacheryCardType.TakeDiscarded);
            RecentMilestones.Add(Milestone.CardWonSwapped);
        }

        private Phase PhaseBeforeSearchingDiscarded { get; set; }
        public void HandleEvent(DiscardedSearchedAnnounced e)
        {
            CurrentReport.Express(e);
            PhaseBeforeSearchingDiscarded = CurrentPhase;
            e.Player.Resources -= 2;
            Enter(Phase.SearchingDiscarded);
            RecentMilestones.Add(Milestone.CardWonSwapped);
        }

        public void HandleEvent(DiscardedSearched e)
        {
            CurrentReport.Express(e);
            foreach (var p in Players)
            {
                UnregisterKnown(p, TreacheryDiscardPile.Items);
            }
            TreacheryDiscardPile.Items.Remove(e.Card);
            e.Player.TreacheryCards.Add(e.Card);
            TreacheryDiscardPile.Shuffle();
            Discard(e.Player, TreacheryCardType.SearchDiscarded);
            Enter(PhaseBeforeSearchingDiscarded);
            RecentMilestones.Add(Milestone.Shuffled);
        }

        private void ExchangeResourcesInBribe(Player from, Player target, int amount)
        {
            from.Resources -= amount;

            if (BribesDuringMentat)
            {
                if (from.Faction == Faction.Red && target.Faction == from.Ally)
                {
                    target.Resources += amount;
                }
                else
                {
                    target.Bribes += amount;
                }
            }
            else
            {
                target.Resources += amount;
            }
        }

        private Phase phasePausedByClairvoyance;
        public ClairVoyancePlayed LatestClairvoyance;
        public ClairVoyanceQandA LatestClairvoyanceQandA;
        public BattleInitiated LatestClairvoyanceBattle;

        public void HandleEvent(ClairVoyancePlayed e)
        {
            var initiator = GetPlayer(e.Initiator);
            var card = initiator.Card(TreacheryCardType.Clairvoyance);

            if (card != null)
            {
                Discard(card);
                CurrentReport.Express(e);
                RecentMilestones.Add(Milestone.Clairvoyance);
            }

            if (e.Target != Faction.None)
            {
                LatestClairvoyance = e;
                LatestClairvoyanceQandA = null;
                LatestClairvoyanceBattle = CurrentBattle;
                phasePausedByClairvoyance = CurrentPhase;
                Enter(Phase.Clairvoyance);
            }
        }

        public void HandleEvent(ClairVoyanceAnswered e)
        {
            LatestClairvoyanceQandA = new ClairVoyanceQandA(LatestClairvoyance, e);
            CurrentReport.Express(e);

            if (LatestClairvoyance.Question == ClairvoyanceQuestion.WillAttackX && e.Answer == ClairVoyanceAnswer.No)
            {
                var deal = new Deal() { Type = DealType.DontShipOrMoveTo, BoundFaction = e.Initiator, ConsumingFaction = LatestClairvoyance.Initiator, DealParameter1 = LatestClairvoyance.QuestionParameter1, End = Phase.ShipmentAndMoveConcluded };
                StartDeal(deal);
            }

            if (LatestClairvoyance.Question == ClairvoyanceQuestion.LeaderAsTraitor)
            {
                var hero = LatestClairvoyance.Parameter1 as IHero;

                if (e.Answer == ClairVoyanceAnswer.Yes)
                {
                    if (e.Player.Traitors.Contains(hero) && !e.Player.ToldTraitors.Contains(hero))
                    {
                        e.Player.ToldTraitors.Add(hero);
                    }
                }
                else if (e.Answer == ClairVoyanceAnswer.No)
                {
                    if (e.Player.Traitors.Contains(hero) && !e.Player.ToldNonTraitors.Contains(hero))
                    {
                        e.Player.ToldNonTraitors.Add(hero);
                    }
                }
            }

            if (LatestClairvoyance.Question == ClairvoyanceQuestion.LeaderAsFacedancer)
            {
                var hero = LatestClairvoyance.Parameter1 as IHero;

                if (e.Answer == ClairVoyanceAnswer.Yes)
                {
                    if (e.Player.Traitors.Contains(hero) && !e.Player.ToldFacedancers.Contains(hero))
                    {
                        e.Player.ToldFacedancers.Add(hero);
                    }
                }
                else if (e.Answer == ClairVoyanceAnswer.No)
                {
                    if (e.Player.Traitors.Contains(hero) && !e.Player.ToldNonFacedancers.Contains(hero))
                    {
                        e.Player.ToldNonFacedancers.Add(hero);
                    }
                }
            }

            Enter(phasePausedByClairvoyance);
        }

        public void HandleEvent(AmalPlayed e)
        {
            Discard(GetPlayer(e.Initiator), TreacheryCardType.Amal);
            CurrentReport.Express(e);
            foreach (var p in Players)
            {
                int resourcesPaid = (int)Math.Ceiling(0.5 * p.Resources);
                p.Resources -= resourcesPaid;
                CurrentReport.Express(p.Faction, " lose ", Payment(resourcesPaid));
            }
            RecentMilestones.Add(Milestone.Amal);
        }

        public void HandleEvent(BrownDiscarded e)
        {
            Discard(e.Card);
            CurrentReport.Express(e);
            if (e.Card.Type == TreacheryCardType.Useless)
            {
                e.Player.Resources += 2;
            }
            else
            {
                e.Player.Resources += 3;
            }

            RecentMilestones.Add(Milestone.ResourcesReceived);
        }


        public int KarmaHmsMovesLeft { get; private set; } = 2;
        public void HandleEvent(KarmaHmsMovement e)
        {
            var initiator = GetPlayer(e.Initiator);
            int collectionRate = initiator.AnyForcesIn(Map.HiddenMobileStronghold) * 2;
            CurrentReport.Express(e);

            if (!initiator.SpecialKarmaPowerUsed)
            {
                Discard(initiator, TreacheryCardType.Karma);
                initiator.SpecialKarmaPowerUsed = true;
            }

            var currentLocation = Map.HiddenMobileStronghold.AttachedToLocation;
            CollectSpiceFrom(e.Initiator, currentLocation, collectionRate);

            if (!e.Passed)
            {
                Map.HiddenMobileStronghold.PointAt(e.Target);
                CollectSpiceFrom(e.Initiator, e.Target, collectionRate);
                KarmaHmsMovesLeft--;
                RecentMilestones.Add(Milestone.HmsMovement);
            }

            if (e.Passed)
            {
                KarmaHmsMovesLeft = 0;
            }
        }

        public void HandleEvent(KarmaBrownDiscard e)
        {
            RecentMilestones.Add(Milestone.Discard);
            Discard(e.Player, TreacheryCardType.Karma);
            CurrentReport.Express(e);

            foreach (var card in e.Cards)
            {
                Discard(e.Player, card);
            }

            e.Player.Resources += e.Cards.Count() * 3;
            e.Player.SpecialKarmaPowerUsed = true;
        }

        public void HandleEvent(KarmaWhiteBuy e)
        {
            RecentMilestones.Add(Milestone.AuctionWon);
            Discard(e.Player, TreacheryCardType.Karma);
            CurrentReport.Express(e);
            e.Player.TreacheryCards.Add(e.Card);
            WhiteCache.Remove(e.Card);
            e.Player.Resources -= 3;
            e.Player.SpecialKarmaPowerUsed = true;
        }

        public void HandleEvent(KarmaFreeRevival e)
        {
            RecentMilestones.Add(Milestone.Revival);
            CurrentReport.Express(e);
            var initiator = GetPlayer(e.Initiator);

            Discard(initiator, TreacheryCardType.Karma);
            initiator.SpecialKarmaPowerUsed = true;

            if (e.Hero != null)
            {
                ReviveHero(e.Hero);

                if (e.AssignSkill)
                {
                    PrepareSkillAssignmentToRevivedLeader(e.Player, e.Hero as Leader);
                }

            }
            else
            {
                initiator.ReviveForces(e.AmountOfForces);
                initiator.ReviveSpecialForces(e.AmountOfSpecialForces);

                if (e.AmountOfSpecialForces > 0)
                {
                    FactionsThatRevivedSpecialForcesThisTurn.Add(e.Initiator);
                }
            }
        }

        public void HandleEvent(KarmaMonster e)
        {
            var initiator = GetPlayer(e.Initiator);
            Discard(initiator, TreacheryCardType.Karma);
            initiator.SpecialKarmaPowerUsed = true;
            CurrentReport.Express(e);
            RecentMilestones.Add(Milestone.Karma);
            NumberOfMonsters++;
            ProcessMonsterCard(e.Territory);

            if (CurrentPhase == Phase.BlowReport)
            {
                Enter(Phase.AllianceB);
            }
        }

        public bool GreenKarma { get; private set; } = false;
        public void HandleEvent(KarmaPrescience e)
        {
            var initiator = GetPlayer(e.Initiator);
            Discard(initiator, TreacheryCardType.Karma);
            initiator.SpecialKarmaPowerUsed = true;
            CurrentReport.Express(e);
            RecentMilestones.Add(Milestone.Karma);
            GreenKarma = true;
        }

        public void HandleEvent(Karma e)
        {
            Discard(e.Card);
            CurrentReport.Express(e);
            RecentMilestones.Add(Milestone.Karma);

            if (e.Prevented != FactionAdvantage.None)
            {
                Prevent(e.Initiator, e.Prevented);
            }

            RevokeBattlePlansIfNeeded(e);

            if (e.Prevented == FactionAdvantage.BlueUsingVoice)
            {
                CurrentVoice = null;
            }

            if (e.Prevented == FactionAdvantage.GreenBattlePlanPrescience)
            {
                CurrentPrescience = null;
            }
        }

        public IList<Faction> SecretsRemainHidden = new List<Faction>();
        public void HandleEvent(HideSecrets e)
        {
            SecretsRemainHidden.Add(e.Initiator);
        }

        public bool YieldsSecrets(Player p)
        {
            return !SecretsRemainHidden.Contains(p.Faction);
        }
        public bool YieldsSecrets(Faction f)
        {
            return !SecretsRemainHidden.Contains(f);
        }

        private void RevokeBattlePlansIfNeeded(Karma e)
        {
            if (CurrentMainPhase == MainPhase.Battle)
            {
                if (e.Prevented == FactionAdvantage.YellowNotPayingForBattles ||
                    e.Prevented == FactionAdvantage.YellowSpecialForceBonus)
                {
                    RevokePlanIfNeeded(Faction.Yellow);
                }

                if (e.Prevented == FactionAdvantage.RedSpecialForceBonus)
                {
                    RevokePlanIfNeeded(Faction.Red);
                }

                if (e.Prevented == FactionAdvantage.GreySpecialForceBonus)
                {
                    RevokePlanIfNeeded(Faction.Grey);
                }

                if (e.Prevented == FactionAdvantage.GreenUseMessiah)
                {
                    RevokePlanIfNeeded(Faction.Green);
                }
            }
        }

        private void RevokePlanIfNeeded(Faction f)
        {
            RevokePlan(CurrentBattle?.PlanOf(f));
        }

        private void RevokePlan(Battle plan)
        {
            if (plan == AggressorBattleAction)
            {
                AggressorBattleAction = null;
            }
            else if (plan == DefenderBattleAction)
            {
                DefenderBattleAction = null;
            }
        }

        private List<FactionAdvantage> PreventedAdvantages = new List<FactionAdvantage>();

        private void Prevent(Faction initiator, FactionAdvantage advantage)
        {
            if (!PreventedAdvantages.Contains(advantage))
            {
                PreventedAdvantages.Add(advantage);
                CurrentReport.Express("Using ", TreacheryCardType.Karma, ", ", initiator, " prevent ", advantage);
            }
        }

        private void Allow(FactionAdvantage advantage)
        {
            if (PreventedAdvantages.Contains(advantage))
            {
                PreventedAdvantages.Remove(advantage);
                CurrentReport.Express(TreacheryCardType.Karma, " no longer prevents ", advantage);
            }
        }

        public bool Prevented(FactionAdvantage advantage) => PreventedAdvantages.Contains(advantage);

        public bool BribesDuringMentat => !Applicable(Rule.BribesAreImmediate);

        public void HandleEvent(PlayerReplaced e)
        {
            GetPlayer(e.ToReplace).IsBot = !GetPlayer(e.ToReplace).IsBot;
            CurrentReport.Express(e.ToReplace, " will now be played by a ", GetPlayer(e.ToReplace).IsBot ? "Bot" : "Human");
        }

        public bool KarmaPrevented(Faction f)
        {
            return CurrentKarmaPrevention != null && CurrentKarmaPrevention.Target == f;
        }

        public BrownKarmaPrevention CurrentKarmaPrevention { get; set; } = null;
        public void HandleEvent(BrownKarmaPrevention e)
        {
            CurrentReport.Express(e);
            Discard(e.CardUsed());
            CurrentKarmaPrevention = e;
            RecentMilestones.Add(Milestone.SpecialUselessPlayed);
        }

        public bool JuiceForcesFirstPlayer => CurrentJuice != null && CurrentJuice.Type == JuiceType.GoFirst;

        public bool JuiceForcesLastPlayer => CurrentJuice != null && CurrentJuice.Type == JuiceType.GoLast;


        public JuicePlayed CurrentJuice { get; set; }
        public void HandleEvent(JuicePlayed e)
        {
            CurrentReport.Express(e);

            var aggressorBeforeJuiceIsPlayed = CurrentBattle?.AggressivePlayer;

            CurrentJuice = e;
            Discard(e.Player, TreacheryCardType.Juice);

            if ((e.Type == JuiceType.GoFirst || e.Type == JuiceType.GoLast) && Version <= 117)
            {
                switch (CurrentMainPhase)
                {
                    case MainPhase.Bidding: BidSequence.CheckCurrentPlayer(); break;
                    case MainPhase.ShipmentAndMove: ShipmentAndMoveSequence.CheckCurrentPlayer(); break;
                    case MainPhase.Battle: BattleSequence.CheckCurrentPlayer(); break;
                }
            }
            else if (CurrentBattle != null && e.Type == JuiceType.Aggressor && CurrentBattle.AggressivePlayer != aggressorBeforeJuiceIsPlayed)
            {
                var currentAggressorBattleAction = AggressorBattleAction;
                var currentAggressorTraitorAction = AggressorTraitorAction;
                AggressorBattleAction = DefenderBattleAction;
                AggressorTraitorAction = DefenderTraitorAction;
                DefenderBattleAction = currentAggressorBattleAction;
                DefenderTraitorAction = currentAggressorTraitorAction;
            }
        }

        private bool BureaucratWasUsedThisPhase { get; set; } = false;
        private Phase _phaseBeforeBureaucratWasActivated;
        public Faction TargetOfBureaucracy { get; private set; }

        private void ApplyBureaucracy(Faction payer, Faction receiver)
        {
            if (!BureaucratWasUsedThisPhase)
            {
                var bureaucrat = PlayerSkilledAs(LeaderSkill.Bureaucrat);
                if (bureaucrat != null && bureaucrat.Faction != payer && bureaucrat.Faction != receiver)
                {
                    if (Version < 133) BureaucratWasUsedThisPhase = true;
                    _phaseBeforeBureaucratWasActivated = CurrentPhase;
                    TargetOfBureaucracy = receiver;
                    Enter(Phase.Bureaucracy);
                }
            }
        }

        public void HandleEvent(Bureaucracy e)
        {
            CurrentReport.Express(e.GetDynamicMessage());
            if (!e.Passed)
            {
                BureaucratWasUsedThisPhase = true;
                GetPlayer(TargetOfBureaucracy).Resources -= 2;
            }
            Enter(_phaseBeforeBureaucratWasActivated);
            TargetOfBureaucracy = Faction.None;
        }

        private bool BankerWasUsedThisPhase { get; set; } = false;

        private void ActivateBanker(Player playerWhoPaid)
        {
            if (!BankerWasUsedThisPhase)
            {
                var banker = PlayerSkilledAs(LeaderSkill.Banker);
                if (banker != null && banker != playerWhoPaid)
                {
                    BankerWasUsedThisPhase = true;
                    CurrentReport.Express(banker.Faction, " will receive ", Payment(1), " from ", LeaderSkill.Banker, " at ", MainPhase.Collection);
                    banker.BankedResources += 1;
                }
            }
        }

        public Planetology CurrentPlanetology { get; private set; }
        public void HandleEvent(Planetology e)
        {
            CurrentReport.Express(e);
            CurrentPlanetology = e;
        }

        private void LogPrevention(FactionAdvantage prevented)
        {
            CurrentReport.Express(TreacheryCardType.Karma, " prevents ", prevented);
        }
    }

    class TriggeredBureaucracy
    {
        internal Faction PaymentFrom;
        internal Faction PaymentTo;
    }
}
