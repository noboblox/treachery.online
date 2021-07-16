﻿/*
 * Copyright 2020-2021 Ronald Ossendrijver. All rights reserved.
 */

using Newtonsoft.Json;

namespace Treachery.Shared
{
    public class BlackMarketBid : GameEvent
    {
        public int Amount { get; set; }

        public int AllyContributionAmount { get; set; }

        public int RedContributionAmount { get; set; }

        [JsonIgnore]
        public int TotalAmount
        {
            get
            {
                return Amount + AllyContributionAmount + RedContributionAmount;
            }
        }

        public bool Passed { get; set; }


        public BlackMarketBid(Game game) : base(game)
        {
        }

        public BlackMarketBid()
        {
        }

        public override string Validate()
        {
            if (Passed) return "";

            var p = Game.GetPlayer(Initiator);

            //if (TotalAmount < 1) return "Bid must be higher than 0.";
            if (Game.CurrentBid != null && TotalAmount <= Game.CurrentBid.TotalAmount) return "Bid not high enough.";

            var ally = Game.GetPlayer(p.Ally);
            if (AllyContributionAmount > 0 && AllyContributionAmount > ally.Resources) return "Your ally can't pay that much.";

            var red = Game.GetPlayer(Faction.Red);
            if (RedContributionAmount > 0 && RedContributionAmount > red.Resources) return Skin.Current.Format("{0} can't pay that much.", Faction.Red);

            return "";
        }

        protected override void ExecuteConcreteEvent()
        {
            Game.HandleEvent(this);
        }

        public override Message GetMessage()
        {
            if (!Passed)
            {
                return new Message(Initiator, "{0} bid {1}.", Initiator, TotalAmount);
            }
            else
            {
                return new Message(Initiator, "{0} pass.", Initiator);
            }
        }

        public static int ValidMaxAmount(Player p)
        {
            return p.Resources;
        }

        public static int ValidMaxAllyAmount(Game g, Player p)
        {
            return g.SpiceYourAllyCanPay(p);
        }
    }
}