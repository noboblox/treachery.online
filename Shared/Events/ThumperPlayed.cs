﻿/*
 * Copyright 2020-2022 Ronald Ossendrijver. All rights reserved.
 */

namespace Treachery.Shared
{
    public class ThumperPlayed : GameEvent
    {
        public ThumperPlayed(Game game) : base(game)
        {
        }

        public ThumperPlayed()
        {
        }

        public override Message Validate()
        {
            return "";
        }

        protected override void ExecuteConcreteEvent()
        {
            Game.HandleEvent(this);
        }

        public override Message GetMessage()
        {
            return Message.Express(Initiator, " use a ", TreacheryCardType.Thumper, " to attract ", Concept.Monster);
        }
    }
}
