﻿/*
 * Copyright 2020-2022 Ronald Ossendrijver. All rights reserved.
 */

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Treachery.Shared
{
    public class FaceDancerReplaced : GameEvent
    {
        public bool Passed { get; set; }

        public int dancerId;

        public FaceDancerReplaced(Game game) : base(game)
        {
        }

        public FaceDancerReplaced()
        {
        }

        [JsonIgnore]
        public IHero SelectedDancer { get { return LeaderManager.HeroLookup.Find(dancerId); } set { dancerId = LeaderManager.HeroLookup.GetId(value); } }

        public override Message Validate()
        {
            if (!Passed)
            {
                var p = Player;
                if (p.RevealedDancers.Contains(SelectedDancer)) return Message.Express("You can't replace a revealed Face Dancer");
                if (!p.FaceDancers.Contains(SelectedDancer)) return Message.Express("Invalid Face Dancer");
            }

            return null;
        }

        protected override void ExecuteConcreteEvent()
        {
            Game.HandleEvent(this);
        }

        public override Message GetMessage()
        {
            return Message.Express(Initiator, MessagePart.ExpressIf(Passed, " don't"), " replace a Face Dancer");
        }

        public static IEnumerable<IHero> ValidFaceDancers(Player p)
        {
            return p.UnrevealedFaceDancers;
        }
    }
}
