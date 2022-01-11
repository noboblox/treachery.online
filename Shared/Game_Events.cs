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
        public IList<Type> GetApplicableEvents(Player player, bool isHost)
        {
            List<Type> result = new List<Type>();

            if (player != null && (CurrentPhase == Phase.SelectingFactions || player.Faction != Faction.None))
            {
                AddPlayerActions(player, isHost, result);
            }

            if (isHost)
            {
                AddHostActions(result);
            }

            return new List<Type>(result);
        }

        private void AddHostActions(List<Type> result)
        {
            switch (CurrentPhase)
            {
                case Phase.AwaitingPlayers:
                    result.Add(typeof(EstablishPlayers));
                    break;
                case Phase.CustomizingDecks:
                    result.Add(typeof(CardsDetermined));
                    break;
                case Phase.PerformCustomSetup:
                    result.Add(typeof(PerformSetup));
                    break;
                case Phase.HmsPlacement:
                    if (!IsPlaying(Faction.Grey) && Applicable(Rule.HMSwithoutGrey)) result.Add(typeof(PerformHmsPlacement));
                    break;
                case Phase.SelectingFactions:
                case Phase.TradingFactions:
                case Phase.MetheorAndStormSpell:
                case Phase.StormReport:
                case Phase.Thumper:
                case Phase.HarvesterA:
                case Phase.HarvesterB:
                case Phase.AllianceA:
                case Phase.AllianceB:
                case Phase.BlowReport:
                case Phase.BeginningOfCharity:
                case Phase.ClaimingCharity:
                case Phase.CharityReport:
                case Phase.WaitingForNextBiddingRound:
                case Phase.BiddingReport:
                case Phase.BeginningOfResurrection:
                case Phase.Resurrection:
                case Phase.ResurrectionReport:
                case Phase.BeginningOfShipAndMove:
                case Phase.ShipmentAndMoveConcluded:
                case Phase.BeginningOfBattle:
                case Phase.BattleReport:
                case Phase.BeginningOfCollection:
                case Phase.CollectionReport:
                case Phase.Contemplate:
                case Phase.TurnConcluded:
                    result.Add(typeof(EndPhase));
                    break;
            }

            if (CurrentMainPhase >= MainPhase.Setup)
            {
                result.Add(typeof(PlayerReplaced));
            }
        }

        private void AddPlayerActions(Player player, bool isHost, List<Type> result)
        {
            var faction = player.Faction;

            switch (CurrentPhase)
            {
                case Phase.Bureaucracy:
                    if (player == PlayerSkilledAs(LeaderSkill.Bureaucrat)) result.Add(typeof(Bureaucracy));
                    break;
                case Phase.AssigningInitialSkills:
                case Phase.AssigningSkill:
                    if (player.SkillsToChooseFrom.Any()) result.Add(typeof(SkillAssigned));
                    break;
                case Phase.SelectingFactions:
                    if (player.Faction == Faction.None) result.Add(typeof(FactionSelected));
                    break;
                case Phase.DiallingStorm:
                    if (HasBattleWheel.Contains(player.Faction) && !HasActedOrPassed.Contains(player.Faction)) result.Add(typeof(StormDialled));
                    break;
                case Phase.TradingFactions:
                    if (Players.Count > 1) result.Add(typeof(FactionTradeOffered));
                    break;
                case Phase.PerformCustomSetup:
                    break;
                case Phase.BluePredicting:
                    if (faction == Faction.Blue) result.Add(typeof(BluePrediction));
                    break;
                case Phase.YellowSettingUp:
                    if (faction == Faction.Yellow) result.Add(typeof(PerformYellowSetup));
                    break;
                case Phase.BlueSettingUp:
                    if (faction == Faction.Blue) result.Add(typeof(PerformBluePlacement));
                    break;
                case Phase.BlackMulligan:
                    if (faction == Faction.Black) result.Add(typeof(MulliganPerformed));
                    break;
                case Phase.SelectingTraitors:
                    if (faction != Faction.Black && faction != Faction.Purple && !HasActedOrPassed.Contains(faction)) result.Add(typeof(TraitorsSelected));
                    break;
                case Phase.GreySelectingCard:
                    if (faction == Faction.Grey) result.Add(typeof(GreySelectedStartingCard));
                    break;
                case Phase.HmsPlacement:
                    if (faction == Faction.Grey) result.Add(typeof(PerformHmsPlacement));
                    break;
                case Phase.HmsMovement:
                    if (faction == Faction.Grey) result.Add(typeof(PerformHmsMovement));
                    break;
                case Phase.MetheorAndStormSpell:
                    if (player.Has(TreacheryCardType.StormSpell) && CurrentTurn > 1) result.Add(typeof(StormSpellPlayed));
                    if (MetheorPlayed.MayPlayMetheor(this, player)) result.Add(typeof(MetheorPlayed));
                    break;
                case Phase.Thumper:
                    if (player.Has(TreacheryCardType.Thumper) && CurrentTurn > 1) result.Add(typeof(ThumperPlayed));
                    if (Version < 103 && player.Has(TreacheryCardType.Amal)) result.Add(typeof(AmalPlayed));
                    break;
                case Phase.HarvesterA:
                case Phase.HarvesterB:
                    if (player.Has(TreacheryCardType.Harvester)) result.Add(typeof(HarvesterPlayed));
                    break;
                case Phase.StormLosses:
                    if (faction == TakeLosses.LossesToTake(this).Faction) result.Add(typeof(TakeLosses));
                    break;
                case Phase.YellowSendingMonsterA:
                case Phase.YellowSendingMonsterB:
                    if (faction == Faction.Yellow) result.Add(typeof(YellowSentMonster));
                    break;
                case Phase.YellowRidingMonsterA:
                case Phase.YellowRidingMonsterB:
                    if (faction == Faction.Yellow) result.Add(typeof(YellowRidesMonster));
                    break;
                case Phase.AllianceA:
                case Phase.AllianceB:
                    if (player.Ally == Faction.None && Players.Count > 1) result.Add(typeof(AllianceOffered));
                    if (player.Ally != Faction.None) result.Add(typeof(AllianceBroken));
                    break;
                case Phase.BlowReport:
                    break;
                case Phase.ClaimingCharity:
                    if (!isHost && faction == Faction.Green) result.Add(typeof(EndPhase));
                    if (player.Resources <= 1 && !HasActedOrPassed.Contains(faction)) result.Add(typeof(CharityClaimed));
                    if (Version < 103 && player.Has(TreacheryCardType.Amal) && (Version <= 82 || HasActedOrPassed.Count == 0)) result.Add(typeof(AmalPlayed));
                    break;
                case Phase.BlackMarketAnnouncement:
                    if (faction == Faction.White) result.Add(typeof(WhiteAnnouncesBlackMarket));
                    break;
                case Phase.BlackMarketBidding:
                    if (BlackMarketBid.MayBePlayed(this, player))
                    {
                        result.Add(typeof(BlackMarketBid));
                    }
                    if (faction == Faction.Red && Applicable(Rule.RedSupportingNonAllyBids)) result.Add(typeof(RedBidSupport));
                    break;
                case Phase.WhiteAnnouncingAuction:
                    if (faction == Faction.White) result.Add(typeof(WhiteAnnouncesAuction));
                    break;
                case Phase.WhiteSpecifyingAuction:
                    if (faction == Faction.White) result.Add(typeof(WhiteSpecifiesAuction));
                    break;
                case Phase.Bidding:
                    if (Bid.MayBePlayed(this, player))
                    {
                        result.Add(typeof(Bid));
                    }
                    if (Version < 103 && player.Has(TreacheryCardType.Amal) && CardNumber == 1 && !Bids.Any()) result.Add(typeof(AmalPlayed));
                    if (faction == Faction.Red && Applicable(Rule.RedSupportingNonAllyBids)) result.Add(typeof(RedBidSupport));
                    break;
                case Phase.WhiteKeepingUnsoldCard:
                    if (faction == Faction.White) result.Add(typeof(WhiteKeepsUnsoldCard));
                    break;
                case Phase.GreyRemovingCardFromBid:
                    if (faction == Faction.Grey) result.Add(typeof(GreyRemovedCardFromAuction));
                    if (Version < 103 && player.Has(TreacheryCardType.Amal) && CardNumber == 1 && !Bids.Any()) result.Add(typeof(AmalPlayed));
                    break;
                case Phase.GreySwappingCard:
                    if (faction == Faction.Grey) result.Add(typeof(GreySwappedCardOnBid));
                    if (Version < 103 && player.Has(TreacheryCardType.Amal) && CardNumber == 1 && !Bids.Any()) result.Add(typeof(AmalPlayed));
                    break;
                case Phase.ReplacingCardJustWon:
                    if (player.Ally == Faction.Grey) result.Add(typeof(ReplacedCardWon));
                    break;
                case Phase.WaitingForNextBiddingRound:
                    if (!isHost && faction == Faction.Green) result.Add(typeof(EndPhase));
                    break;
                case Phase.BiddingReport:
                    if (faction == Faction.Purple && Players.Count > 1 && (Version < 113 || !Prevented(FactionAdvantage.PurpleIncreasingRevivalLimits))) result.Add(typeof(SetIncreasedRevivalLimits));
                    break;
                case Phase.Resurrection:
                    if (IsPlaying(Faction.Purple) && faction != Faction.Purple &&
                        (Version <= 78 || !HasActedOrPassed.Contains(faction)) &&
                        KilledHeroes(player).Any() &&
                        !Revival.NormallyRevivableHeroes(this, player).Any() &&
                        (Version < 102 || CurrentPurpleRevivalRequest == null)) result.Add(typeof(RequestPurpleRevival));

                    if (!HasActedOrPassed.Contains(faction) && HasSomethingToRevive(player)) result.Add(typeof(Revival));
                    if (faction == Faction.Purple && Players.Count > 1 && (Version < 113 || !Prevented(FactionAdvantage.PurpleIncreasingRevivalLimits))) result.Add(typeof(SetIncreasedRevivalLimits));
                    if (faction == Faction.Purple && (CurrentPurpleRevivalRequest != null || AllowedEarlyRevivals.Any())) result.Add(typeof(AcceptOrCancelPurpleRevival));
                    if (Version < 103 && player.Has(TreacheryCardType.Amal) && (Version <= 82 || HasActedOrPassed.Count == 0)) result.Add(typeof(AmalPlayed));
                    break;
                case Phase.BeginningOfResurrection:
                    if (faction == Faction.Purple && Players.Count > 1 && (Version < 113 || !Prevented(FactionAdvantage.PurpleIncreasingRevivalLimits))) result.Add(typeof(SetIncreasedRevivalLimits));
                    break;
                case Phase.OrangeShip:
                    if (faction == Faction.Orange)
                    {
                        if (OrangeMayDelay) result.Add(typeof(OrangeDelay));
                        result.Add(typeof(Shipment));
                        if (player.TreacheryCards.Any(c => c.Type == TreacheryCardType.Caravan)) result.Add(typeof(Caravan));
                    }
                    if (Version < 103 && Version <= 96 && player.Has(TreacheryCardType.Amal) && HasActedOrPassed.Count == 0) result.Add(typeof(AmalPlayed));
                    if (Version < 103 && Version >= 97 && player.Has(TreacheryCardType.Amal) && BeginningOfShipmentAndMovePhase) result.Add(typeof(AmalPlayed));
                    break;
                case Phase.BlueAccompaniesOrange:
                case Phase.BlueAccompaniesNonOrange:
                    if (faction == Faction.Blue) result.Add(typeof(BlueAccompanies));
                    break;
                case Phase.NonOrangeShip:
                    if (player == ShipmentAndMoveSequence.CurrentPlayer)
                    {
                        result.Add(typeof(Shipment));
                        if (player.TreacheryCards.Any(c => c.Type == TreacheryCardType.Caravan)) result.Add(typeof(Caravan));
                    }
                    if (Version < 103 && Version <= 96 && player.Has(TreacheryCardType.Amal) && HasActedOrPassed.Count == 0) result.Add(typeof(AmalPlayed));
                    if (Version < 103 && Version >= 97 && player.Has(TreacheryCardType.Amal) && BeginningOfShipmentAndMovePhase) result.Add(typeof(AmalPlayed));
                    break;
                case Phase.OrangeMove:
                    if (faction == Faction.Orange)
                    {
                        result.Add(typeof(Move));
                        if (player.TreacheryCards.Any(c => c.Type == TreacheryCardType.Caravan)) result.Add(typeof(Caravan));
                        if (FlightUsed.IsAvailable(player)) result.Add(typeof(FlightUsed));
                        if (Planetology.CanBePlayed(this, player)) result.Add(typeof(Planetology));
                    }
                    break;
                case Phase.NonOrangeMove:
                    if (player == ShipmentAndMoveSequence.CurrentPlayer)
                    {
                        result.Add(typeof(Move));
                        if (player.TreacheryCards.Any(c => c.Type == TreacheryCardType.Caravan)) result.Add(typeof(Caravan));
                        if (FlightUsed.IsAvailable(player)) result.Add(typeof(FlightUsed));
                        if (Planetology.CanBePlayed(this, player)) result.Add(typeof(Planetology));
                    }
                    break;
                case Phase.BlueIntrudedByOrangeShip:
                case Phase.BlueIntrudedByNonOrangeShip:
                case Phase.BlueIntrudedByOrangeMove:
                case Phase.BlueIntrudedByNonOrangeMove:
                case Phase.BlueIntrudedByYellowRidingMonsterA:
                case Phase.BlueIntrudedByYellowRidingMonsterB:
                case Phase.BlueIntrudedByCaravan:
                    if (faction == Faction.Blue) result.Add(typeof(BlueFlip));
                    break;
                case Phase.ShipmentAndMoveConcluded:
                    if (Version < 103 && player.Has(TreacheryCardType.Amal)) result.Add(typeof(AmalPlayed));
                    break;

                case Phase.BattlePhase:
                    {
                        if (CurrentBattle == null && player == NextPlayerToBattle)
                        {
                            result.Add(typeof(BattleInitiated));
                        }

                        if (CurrentBattle != null && SwitchedSkilledLeader.CanBePlayed(this, player))
                        {
                            result.Add(typeof(SwitchedSkilledLeader));
                        }

                        if (HMSAdvantageChosen.CanBePlayed(this, player))
                        {
                            result.Add(typeof(HMSAdvantageChosen));
                        }

                        if (CurrentBattle != null && faction == CurrentBattle.Aggressor && AggressorBattleAction == null)
                        {
                            result.Add(typeof(Battle));
                        }
                        else if (CurrentBattle != null && faction == CurrentBattle.Defender && DefenderBattleAction == null)
                        {
                            result.Add(typeof(Battle));
                        }

                        if (CurrentBattle != null && faction == CurrentBattle.Aggressor && AggressorBattleAction != null)
                        {
                            result.Add(typeof(BattleRevision));
                        }
                        else if (CurrentBattle != null && faction == CurrentBattle.Defender && DefenderBattleAction != null)
                        {
                            result.Add(typeof(BattleRevision));
                        }

                        if (Voice.MayUseVoice(this, player))
                        {
                            result.Add(typeof(Voice));
                        }

                        if (Prescience.MayUsePrescience(this, player))
                        {
                            result.Add(typeof(Prescience));
                        }

                        if (Thought.MayBeUsed(this, player))
                        {
                            result.Add(typeof(Thought));
                        }

                        if (CurrentBattle != null && ResidualPlayed.MayPlay(this, player))
                        {
                            result.Add(typeof(ResidualPlayed));
                        }

                        if (Version < 103 && player.Has(TreacheryCardType.Amal) && NrOfBattlesFought == 0) result.Add(typeof(AmalPlayed));
                    }
                    break;

                case Phase.CallTraitorOrPass:

                    if (AggressorBattleAction != null && DefenderBattleAction != null &&
                            (AggressorTraitorAction == null && faction == CurrentBattle.Aggressor ||
                             AggressorTraitorAction == null && faction == Faction.Black && GetPlayer(AggressorBattleAction.Initiator).Ally == Faction.Black && player.Traitors.Contains(DefenderBattleAction.Hero) ||
                             DefenderTraitorAction == null && faction == CurrentBattle.Defender ||
                             DefenderTraitorAction == null && faction == Faction.Black && GetPlayer(DefenderBattleAction.Initiator).Ally == Faction.Black && player.Traitors.Contains(AggressorBattleAction.Hero)))
                    {
                        result.Add(typeof(TreacheryCalled));
                    }

                    if (faction == AggressorBattleAction.Initiator && AggressorBattleAction.Weapon != null && AggressorBattleAction.Weapon.Type == TreacheryCardType.PoisonTooth && !PoisonToothCancelled) result.Add(typeof(PoisonToothCancelled));

                    if (faction == DefenderBattleAction.Initiator && DefenderBattleAction.Weapon != null && DefenderBattleAction.Weapon.Type == TreacheryCardType.PoisonTooth && !PoisonToothCancelled) result.Add(typeof(PoisonToothCancelled));

                    if (PortableAntidoteUsed.CanBePlayed(this, player)) result.Add(typeof(PortableAntidoteUsed));

                    if (Diplomacy.CanBePlayed(this, player)) result.Add(typeof(Diplomacy));

                    if (Retreat.CanBePlayed(this, player)) result.Add(typeof(Retreat));

                    break;

                case Phase.MeltingRock:
                    if (RockWasMelted.CanBePlayed(this, player)) result.Add(typeof(RockWasMelted));
                    break;

                case Phase.CaptureDecision:
                    if (faction == Faction.Black) result.Add(typeof(Captured));
                    break;

                case Phase.BattleConclusion:
                    if (faction == BattleWinner) result.Add(typeof(BattleConcluded));
                    break;

                case Phase.AvoidingAudit:
                    if (player == Auditee) result.Add(typeof(AuditCancelled));
                    break;

                case Phase.Auditing:
                    if (faction == Faction.Brown) result.Add(typeof(Audited));
                    break;

                case Phase.Facedancing:
                    if (faction == Faction.Purple) result.Add(typeof(FaceDanced));
                    break;

                case Phase.BattleReport:
                    if (Version < 103 && player.Has(TreacheryCardType.Amal) && NextPlayerToBattle == null) result.Add(typeof(AmalPlayed));
                    break;

                case Phase.ReplacingFaceDancer:
                    if (faction == Faction.Purple) result.Add(typeof(FaceDancerReplaced));
                    break;

                case Phase.Contemplate:
                    if (Version < 103 && player.Has(TreacheryCardType.Amal)) result.Add(typeof(AmalPlayed));
                    if (faction == Faction.Brown && !Prevented(FactionAdvantage.BrownEconomics) && EconomicsStatus == BrownEconomicsStatus.None) result.Add(typeof(BrownEconomics));
                    break;

                case Phase.PerformingKarmaHandSwap:
                    if (faction == Faction.Black && !KarmaPrevented(faction)) result.Add(typeof(KarmaHandSwap));
                    break;

                case Phase.TradingCards:
                    if (faction == CurrentCardTradeOffer.Target) result.Add(typeof(CardTraded));
                    break;

                case Phase.Clairvoyance:
                    if (faction == LatestClairvoyance.Target) result.Add(typeof(ClairVoyanceAnswered));
                    break;

                case Phase.Thought:
                    if (player == CurrentBattle.OpponentOf(CurrentThought.Initiator)) result.Add(typeof(ThoughtAnswered));
                    break;

                case Phase.SearchingDiscarded:
                    if (DiscardedSearched.CanBePlayed(player)) result.Add(typeof(DiscardedSearched));
                    break;
            }

            //Events that are (amost) always valid
            if (Version <= 123)
            {
                result.Add(typeof(HideSecrets));
            }

            if (player.Ally != Faction.None) result.Add(typeof(AllyPermission));

            bool isAfterSetup = Version < 107 ? CurrentPhase > Phase.TradingFactions : CurrentMainPhase > MainPhase.Setup;
            bool hasFinalizedBattlePlanWaitingToBeResolved = (CurrentPhase == Phase.BattlePhase || CurrentPhase == Phase.MeltingRock || CurrentPhase == Phase.CallTraitorOrPass) && CurrentBattle != null && CurrentBattle.PlanOf(player) != null;

            if (JuicePlayed.CanBePlayedBy(this, player))
            {
                result.Add(typeof(JuicePlayed));
            }

            if (isAfterSetup &&
                (Version < 100 || !hasFinalizedBattlePlanWaitingToBeResolved) &&
                CurrentMainPhase < MainPhase.Ended &&
                CurrentPhase != Phase.AssigningSkill &&
                CurrentPhase != Phase.Clairvoyance &&
                CurrentPhase != Phase.Thought &&
                CurrentPhase != Phase.TradingCards &&
                CurrentPhase != Phase.Bureaucracy &&
                CurrentPhase != Phase.SearchingDiscarded &&
                CurrentPhase != Phase.PerformingKarmaHandSwap &&
                CurrentPhase != Phase.ReplacingCardJustWon)
            {
                if (CurrentMainPhase < MainPhase.Battle && player.NoFieldIsActive)
                {
                    result.Add(typeof(WhiteRevealedNoField));
                }

                if (faction == Faction.Brown && !Prevented(FactionAdvantage.BrownDiscarding) && ConsiderAsEndOfPhase && BrownDiscarded.ValidCards(this, player).Any())
                {
                    result.Add(typeof(BrownDiscarded));
                }

                if (!result.Contains(typeof(AmalPlayed)) && player.Has(TreacheryCardType.Amal) && ConsiderAsStartOfPhase)
                {
                    result.Add(typeof(AmalPlayed));
                }

                if (DiscardedTaken.CanBePlayed(this, player))
                {
                    result.Add(typeof(DiscardedTaken));
                }

                if (CurrentPhase != Phase.SearchingDiscarded && DiscardedSearchedAnnounced.CanBePlayed(this, player))
                {
                    result.Add(typeof(DiscardedSearchedAnnounced));
                }

                if (CurrentMainPhase == MainPhase.ShipmentAndMove && BrownMovePrevention.CanBePlayedBy(this, player))
                {
                    result.Add(typeof(BrownMovePrevention));
                }

                if (BrownKarmaPrevention.CanBePlayedBy(this, player))
                {
                    result.Add(typeof(BrownKarmaPrevention));
                }

                if (CurrentMainPhase == MainPhase.ShipmentAndMove && BrownExtraMove.CanBePlayedBy(this, player))
                {
                    result.Add(typeof(BrownExtraMove));
                }

                if (CurrentMainPhase == MainPhase.Resurrection && BrownFreeRevivalPrevention.CanBePlayedBy(this, player))
                {
                    result.Add(typeof(BrownFreeRevivalPrevention));
                }

                if (CurrentMainPhase == MainPhase.Contemplate && BrownRemoveForce.CanBePlayedBy(this, player))
                {
                    result.Add(typeof(BrownRemoveForce));
                }

                if (
                    (faction == Faction.Brown && player.Ally != Faction.None || player.Ally == Faction.Brown) &&
                    ConsiderAsEndOfPhase &&
                    player.AlliedPlayer.TreacheryCards.Count > 0 &&
                    player.TreacheryCards.Count > 0 &&
                    LastTurnCardWasTraded < CurrentTurn)
                {
                    result.Add(typeof(CardTraded));
                }

                if (player.Has(TreacheryCardType.RaiseDead))
                {
                    result.Add(typeof(RaiseDeadPlayed));
                }

                if (player.Has(TreacheryCardType.Clairvoyance))
                {
                    result.Add(typeof(ClairVoyancePlayed));
                }

                if (player.HasKarma && !KarmaPrevented(faction))
                {
                    result.Add(typeof(Karma));
                }

                if (Players.Count > 1 &&
                    faction == Faction.Black &&
                    !KarmaPrevented(faction) &&
                    !player.SpecialKarmaPowerUsed &&
                    player.Has(TreacheryCardType.Karma) &&
                    CurrentMainPhase == MainPhase.Bidding &&
                    Applicable(Rule.AdvancedKarama))
                {
                    result.Add(typeof(KarmaHandSwapInitiated));
                }

                if (faction == Faction.Orange &&
                    !KarmaPrevented(faction) &&
                    !player.SpecialKarmaPowerUsed &&
                    player.Has(TreacheryCardType.Karma) &&
                    CurrentMainPhase == MainPhase.ShipmentAndMove &&
                    Applicable(Rule.AdvancedKarama))
                {
                    result.Add(typeof(KarmaShipmentPrevention));
                }

                if (faction == Faction.Brown &&
                    !KarmaPrevented(faction) &&
                    !player.SpecialKarmaPowerUsed &&
                    player.Has(TreacheryCardType.Karma) &&
                    Applicable(Rule.AdvancedKarama))
                {
                    result.Add(typeof(KarmaBrownDiscard));
                }

                if (faction == Faction.White &&
                    !KarmaPrevented(faction) &&
                    !player.SpecialKarmaPowerUsed &&
                    player.Has(TreacheryCardType.Karma) &&
                    Applicable(Rule.AdvancedKarama))
                {
                    result.Add(typeof(KarmaWhiteBuy));
                }

                if (faction == Faction.Red &&
                    !KarmaPrevented(faction) &&
                    !player.SpecialKarmaPowerUsed &&
                    player.Has(TreacheryCardType.Karma) &&
                    CurrentMainPhase == MainPhase.Resurrection &&
                    Applicable(Rule.AdvancedKarama))
                {
                    result.Add(typeof(KarmaFreeRevival));
                }

                if (faction == Faction.Grey &&
                    (!KarmaPrevented(faction) && !player.SpecialKarmaPowerUsed && player.Has(TreacheryCardType.Karma) || KarmaHmsMovesLeft == 1) &&
                    CurrentMainPhase == MainPhase.ShipmentAndMove &&
                    player == ShipmentAndMoveSequence.CurrentPlayer &&
                    player.AnyForcesIn(Map.HiddenMobileStronghold) > 0 &&
                    Applicable(Rule.AdvancedKarama))
                {
                    result.Add(typeof(KarmaHmsMovement));
                }

                if (faction == Faction.Yellow && CurrentMainPhase == MainPhase.Blow && CurrentTurn > 1 &&
                    !KarmaPrevented(faction) &&
                    !player.SpecialKarmaPowerUsed &&
                    player.Has(TreacheryCardType.Karma) &&
                    Applicable(Rule.AdvancedKarama))
                {
                    result.Add(typeof(KarmaMonster));
                }

                if (faction == Faction.Green && CurrentMainPhase == MainPhase.Battle &&
                    CurrentBattle != null &&
                    CurrentBattle.IsInvolved(player) &&
                    !KarmaPrevented(faction) &&
                    !player.SpecialKarmaPowerUsed &&
                    player.Has(TreacheryCardType.Karma) &&
                    Applicable(Rule.AdvancedKarama))
                {
                    result.Add(typeof(KarmaPrescience));
                }

                if (faction == Faction.Blue && CurrentPhase > Phase.AllianceB &&
                    CurrentPhase < Phase.NonOrangeShip &&
                    BlueBattleAnnouncement.ValidTerritories(this, player).Any())
                {
                    result.Add(typeof(BlueBattleAnnouncement));
                }

                if (Players.Count > 1 &&
                    Donated.ValidTargets(this, player).Any() &&
                    (isHost || player.Resources > 0) &&
                    Donated.MayDonate(this, player) &&
                    (AggressorBattleAction == null || faction != AggressorBattleAction.Initiator) &&
                    (DefenderBattleAction == null || faction != DefenderBattleAction.Initiator))
                {
                    result.Add(typeof(Donated));
                }

                if (faction == Faction.White &&
                    CurrentPhase != Phase.BlackMarketBidding &&
                    CurrentPhase != Phase.Bidding &&
                    !hasFinalizedBattlePlanWaitingToBeResolved &&
                    player.Ally != Faction.None &&
                    WhiteGaveCard.ValidCards(this, player).Any() &&
                    player.AlliedPlayer.HasRoomForCards)
                {
                    result.Add(typeof(WhiteGaveCard));
                }

                if (CurrentPhase != Phase.BlackMarketBidding &&
                    CurrentPhase != Phase.Bidding &&
                    !hasFinalizedBattlePlanWaitingToBeResolved &&
                    DistransUsed.CanBePlayed(this, player))
                {
                    result.Add(typeof(DistransUsed));
                }

                if (Version >= 110)
                {
                    result.Add(typeof(DealOffered));
                    result.Add(typeof(DealAccepted));
                }
            }

            if (Version < 110)
            {
                if (player.Ally != Faction.None) result.Add(typeof(AllyPermission));

                result.Add(typeof(DealOffered));
                result.Add(typeof(DealAccepted));
            }

        }

        public static IEnumerable<Type> GetGameEventTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies().SelectMany(ass => ass.GetTypes().Where(t => t.IsSubclassOf(typeof(GameEvent))).Distinct());
        }

        private bool ConsiderAsEndOfPhase =>
            CurrentMoment == MainPhaseMoment.End ||
            CurrentMoment == MainPhaseMoment.Start &&
            (CurrentMainPhase == MainPhase.Bidding && CurrentMainPhase == MainPhase.ShipmentAndMove);

        private bool ConsiderAsStartOfPhase =>
            CurrentMoment == MainPhaseMoment.Start ||
            (CurrentMainPhase == MainPhase.Blow && CurrentMoment == MainPhaseMoment.End) ||
            Version < 109 && CurrentMoment == MainPhaseMoment.End;
    }
}
