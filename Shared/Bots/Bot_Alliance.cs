﻿/*
 * Copyright (C) 2020-2024 Ronald Ossendrijver (admin@treachery.online)
 * This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version. This
 * program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details. You should have
 * received a copy of the GNU General Public License along with this program. If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Linq;


namespace Treachery.Shared
{
    public partial class Player
    {
        protected virtual AllianceByTerror DetermineAllianceByTerror()
        {
            var cyan = Game.GetPlayer(Faction.Cyan);
            var accept = !HasAlly || PlayerStanding(cyan) > 0.75f * PlayerStanding(AlliedPlayer);
            return new AllianceByTerror(Game, Faction) { Passed = !accept };
        }

        protected virtual AllianceByAmbassador DetermineAllianceByAmbassador()
        {
            var pink = Game.GetPlayer(Faction.Pink);
            var accept = !HasAlly || PlayerStanding(pink) > 0.75f * PlayerStanding(AlliedPlayer);
            return new AllianceByAmbassador(Game, Faction) { Passed = !accept };
        }

        protected NexusVoted DetermineNexusVoted()
        {
            return new NexusVoted(Game, Faction) { Passed = false };
        }

        protected virtual AllianceOffered DetermineAllianceOffered()
        {
            int nrOfPlayers = Game.Players.Count;
            int nrOfBots = Game.Players.Count(p => p.IsBot);
            int nrOfUnalliedBots = Game.Players.Count(p => p.IsBot && p.Ally == Faction.None);

            int nrOfUnalliedHumans = Game.Players.Count(p => !(p.IsBot) && p.Ally == Faction.None);

            var offer = Game.CurrentAllianceOffers.Where(offer => offer.Target == Faction && !offer.Player.IsBot).HighestOrDefault(offer => PlayerStanding(offer.Player));
            if (offer == null) offer = Game.CurrentAllianceOffers.Where(offer => offer.Target == Faction).HighestOrDefault(offer => PlayerStanding(offer.Player));

            if (offer != null)
            {
                return new AllianceOffered(Game, Faction) { Target = offer.Initiator };
            }
            else if (
                nrOfPlayers > 2 &&
                !Game.Applicable(Rule.BotsCannotAlly) &&
                (nrOfUnalliedHumans == 0 || nrOfUnalliedHumans < nrOfUnalliedBots - 1) &&
                !Game.CurrentAllianceOffers.Any(o => o.Initiator == Faction && Game.GetPlayer(o.Target).Ally == Faction.None))
            {
                var mostInterestingOpponentBotWithoutAlly = Game.Players.Where(p => p != this && p.IsBot && p.Ally == Faction.None).HighestOrDefault(p => PlayerStanding(p));

                if (mostInterestingOpponentBotWithoutAlly != null)
                {
                    return new AllianceOffered(Game, Faction) { Target = mostInterestingOpponentBotWithoutAlly.Faction };
                }
            }

            return null;
        }

        private int PlayerStanding(Player p) =>
                    2 * p.TreacheryCards.Count() +
                    p.Resources +
                    6 * p.LocationsWithAnyForces.Count(l => l.Territory.IsStronghold) +
                    p.ForcesOnPlanet.Sum(b => b.Value.TotalAmountOfForces) +
                    p.ForcesInReserve +
                    2 * p.SpecialForcesInReserve +
                    2 * p.Leaders.Count(l => Game.IsAlive(l));

        protected virtual AllianceBroken DetermineAllianceBroken()
        {
            var offer = Game.CurrentAllianceOffers.Where(offer => offer.Target == Faction && !offer.Player.IsBot).HighestOrDefault(offer => PlayerStanding(offer.Player));

            if (offer != null && PlayerStanding(offer.Player) > PlayerStanding(AlliedPlayer))
            {
                return new AllianceBroken(Game, Faction);
            }

            return null;
        }

        protected virtual NexusCardDrawn DetermineNexusCardDrawn()
        {
            if (NexusCardDrawn.MayDraw(Game, this))
            {
                if (Nexus == Faction.None ||
                    Faction == Faction.Red && Nexus == Faction.Red && !Game.Applicable(Rule.RedSpecialForces) ||
                    Faction == Faction.Blue && Nexus == Faction.Blue && !Game.Applicable(Rule.BlueAdvisors) ||
                    Faction == Faction.Grey && Nexus == Faction.Grey && !Game.Applicable(Rule.AdvancedCombat))
                {
                    return new NexusCardDrawn(Game, Faction) { Passed = false };
                }
            }

            return new NexusCardDrawn(Game, Faction) { Passed = true };
        }

        protected virtual AllyPermission DetermineAlliancePermissions()
        {
            if (Ally == Faction.None) return null;

            if (Game.CurrentMainPhase == MainPhase.Blow || Game.CurrentMainPhase == MainPhase.Bidding || Game.CurrentMainPhase == MainPhase.ShipmentAndMove || Game.CurrentMainPhase == MainPhase.Battle)
            {

                var allowedResources = DetermineAllowedResources();
                var allowedKarmaCard = TreacheryCards.FirstOrDefault(c => c.Type == TreacheryCardType.Karma);
                var permission = new AllyPermission(Game, Faction) { PermittedResources = allowedResources, PermittedKarmaCard = allowedKarmaCard };
                bool boolPermissionsNeedUpdate = BoolPermissionsNeedUpdate(permission);
                bool specialPermissionsNeedUpdate = SpecialPermissionsNeedUpdate(permission);

                if (boolPermissionsNeedUpdate || specialPermissionsNeedUpdate || Game.GetPermittedUseOfAllyResources(Ally) != allowedResources || Game.GetPermittedUseOfAllyKarma(Ally) != allowedKarmaCard)
                {
                    LogInfo("Updating permissions, allowing use of {0} spice and Karama: {1}", allowedResources, allowedKarmaCard);
                    return permission;
                }
            }

            return null;
        }

        private bool SpecialPermissionsNeedUpdate(AllyPermission permission)
        {
            var result = false;

            switch (Faction)
            {
                case Faction.Red:
                    permission.RedWillPayForExtraRevival = 3;
                    if (Game.RedWillPayForExtraRevival != 3)
                    {
                        result = true;
                    }
                    break;
            }

            return result;
        }

        private int DetermineAllowedResources()
        {
            switch (Faction)
            {
                case Faction.Green:
                case Faction.Blue:
                case Faction.White:
                case Faction.Black:
                case Faction.Pink:
                case Faction.Cyan:
                case Faction.Grey:

                    if (Game.CurrentMainPhase == MainPhase.ShipmentAndMove && Game.HasActedOrPassed.Contains(Faction))
                    {
                        return Math.Max(Resources - 5, 0);
                    }
                    else
                    {
                        return Math.Max(Resources - 10, 0);
                    }

                case Faction.Brown:
                    if (Game.CurrentMainPhase == MainPhase.Battle)
                    {
                        return Resources;
                    }
                    else if (Game.CurrentMainPhase == MainPhase.ShipmentAndMove && Game.HasActedOrPassed.Contains(Faction))
                    {
                        return Math.Max(Resources - 5, 0);
                    }
                    else
                    {
                        return Math.Max(Resources - 10, 0);
                    }

                case Faction.Orange:
                    if (Game.CurrentMainPhase == MainPhase.ShipmentAndMove)
                    {
                        return Resources;
                    }
                    else
                    {
                        return Math.Max(Resources - 10, 0);
                    }

                case Faction.Yellow:
                    if (Game.CurrentMainPhase == MainPhase.ShipmentAndMove || TreacheryCards.Count() == 4)
                    {
                        return Resources;
                    }
                    else
                    {
                        return Math.Max(Resources - 10, 0);
                    }

                case Faction.Red:
                    if (Game.CurrentMainPhase == MainPhase.Bidding)
                    {
                        return Resources;
                    }
                    else if (Game.CurrentMainPhase == MainPhase.ShipmentAndMove && Game.HasActedOrPassed.Contains(Faction))
                    {
                        return Math.Max(Resources - 5, 0);
                    }
                    else
                    {
                        return Math.Max(Resources - 10, 0);
                    }

                case Faction.Purple:
                    if (Game.CurrentMainPhase == MainPhase.Resurrection)
                    {
                        return Resources;
                    }
                    else if (Game.CurrentMainPhase == MainPhase.ShipmentAndMove && Game.HasActedOrPassed.Contains(Faction))
                    {
                        return Math.Max(Resources - 5, 0);
                    }
                    else
                    {
                        return Math.Max(Resources - 10, 0);
                    }
            }

            return 0;
        }

        private bool BoolPermissionsNeedUpdate(AllyPermission permission)
        {
            bool result = false;

            foreach (var p in GetPermissionProperties())
            {
                typeof(AllyPermission).GetProperty(p).SetValue(permission, true);

                if (!result && !typeof(Game).GetProperty(p).GetValue(Game).Equals(true))
                {
                    result = true;
                }
            }

            return result;
        }

        private IEnumerable<string> GetPermissionProperties()
        {
            switch (Faction)
            {
                case Faction.Green: return new string[] { "GreenSharesPrescience" };
                case Faction.Yellow: return new string[] { "YellowSharesPrescience", "YellowWillProtectFromMonster", "YellowAllowsThreeFreeRevivals", "YellowRefundsBattleDial" };
                case Faction.Orange: return new string[] { "OrangeAllowsShippingDiscount" };
                case Faction.Blue: return new string[] { "BlueAllowsUseOfVoice" };
                case Faction.Grey: return new string[] { "GreyAllowsReplacingCards" };
                case Faction.Purple: return new string[] { "PurpleAllowsRevivalDiscount" };
                case Faction.White: return new string[] { "WhiteAllowsUseOfNoField" };
                case Faction.Cyan: return new string[] { "CyanAllowsKeepingCards" };
                case Faction.Pink: return new string[] { "PinkSharesAmbassadors" };
            }

            return Array.Empty<string>();
        }

        protected virtual CardTraded DetermineCardTraded()
        {
            if (Game.CurrentCardTradeOffer != null)
            {
                CardTraded result;

                if (Game.CurrentCardTradeOffer.RequestedCard != null)
                {
                    result = new CardTraded(Game, Faction) { Target = Game.CurrentCardTradeOffer.Initiator, Card = Game.CurrentCardTradeOffer.RequestedCard, RequestedCard = null };
                }
                else
                {
                    result = new CardTraded(Game, Faction) { Target = Game.CurrentCardTradeOffer.Initiator, Card = TreacheryCards.OrderBy(c => CardQuality(c, this)).FirstOrDefault(), RequestedCard = null };
                }

                return result;
            }

            return null;
        }

    }

}
