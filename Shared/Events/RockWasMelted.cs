﻿/*
 * Copyright 2020-2021 Ronald Ossendrijver. All rights reserved.
 */

using System;

namespace Treachery.Shared
{
    public class RockWasMelted : GameEvent
    {
        public RockWasMelted(Game game) : base(game)
        {
        }

        public RockWasMelted()
        {
        }

        public bool Kill { get; set; }

        public override string Validate()
        {
            return "";
        }

        protected override void ExecuteConcreteEvent()
        {
            Game.HandleEvent(this);
        }

        public override Message GetMessage()
        {
            if (Kill)
            {
                return new Message(Initiator, "{0} use a {1} to kill both leaders.", Initiator, TreacheryCardType.Rockmelter);
            }
            else
            {
                return new Message(Initiator, "{0} use a {1} to reduce both leaders to 0 strength.", Initiator, TreacheryCardType.Rockmelter);
            }
        }

        public static Territory GetTerritory(Game g)
        {
            return g.LastShippedOrMovedTo.Territory;
        }

        public static bool CanBePlayed(Game g, Player p)
        {
            return g.CurrentBattle.IsAggressorOrDefender(p) && p.Has(TreacheryCardType.Rockmelter);
        }
    }
}
