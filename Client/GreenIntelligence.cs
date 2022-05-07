﻿/*
 * Copyright 2020-2022 Ronald Ossendrijver. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Treachery.Shared;

namespace Treachery.Client
{
    public class GreenIntelligence
    {
        public string TrackedSpiceCard;
        public Dictionary<Faction, Dictionary<int, int>> TrackedTreacheryCards;
        private readonly List<int> discardedCards = new();
        private readonly List<int> removedCards = new();
        public Dictionary<Tuple<Faction, int>, int> trackedTraitors = new();
        public Dictionary<int, int> trackedDiscardedTraitors = new();
        private readonly TreacheryCard[] CardsInPlay;

        public GreenIntelligence(Game g)
        {
            var whiteCards = g.IsPlaying(Faction.White) ? TreacheryCardManager.GetWhiteCards().ToArray() : Array.Empty<TreacheryCard>();
            CardsInPlay = TreacheryCardManager.GetCardsInPlay(g).Union(whiteCards).ToArray();
            TrackedTreacheryCards = new Dictionary<Faction, Dictionary<int, int>>();
            foreach (var p in g.Players)
            {
                var cardsOfPlayer = new Dictionary<int, int>();
                for (int i = 0; i < p.MaximumNumberOfCards; i++)
                {
                    cardsOfPlayer.Add(i, DefaultSelectedCard(p.Faction, i));
                }
                TrackedTreacheryCards.Add(p.Faction, cardsOfPlayer);
            }
        }

        private static int DefaultSelectedCard(Faction p, int cardnr)
        {
            if (cardnr == 0) return TreacheryCard.UNKNOWN;
            if (cardnr == 1 && p == Faction.Black) return TreacheryCard.UNKNOWN;
            return TreacheryCard.NONE;
        }

        public IEnumerable<TreacheryCard> DiscardedCards
        {
            get
            {
                return discardedCards.Select(id => TreacheryCardManager.Lookup.Find(id));
            }
        }

        public IEnumerable<TreacheryCard> RemovedCards
        {
            get
            {
                return removedCards.Select(id => TreacheryCardManager.Lookup.Find(id));
            }
        }

        public IEnumerable<TreacheryCard> AvailableDistinctCards(Faction f, int cardNumber)
        {
            var result = new List<TreacheryCard>();

            var cardsSelectedElsewhere = AllTrackedCardsExcept(f, cardNumber);

            foreach (var c in CardsInPlay)
            {
                if (!result.Any(added => Skin.Current.Describe(added) == Skin.Current.Describe(c)) && !removedCards.Contains(c.Id) && !discardedCards.Contains(c.Id) && !cardsSelectedElsewhere.Contains(c.Id))
                {
                    result.Add(c);
                }
            }

            return result;
        }

        public IEnumerable<int> AllTrackedCards()
        {
            var result = new List<int>();
            foreach (var cardsOfPlayer in TrackedTreacheryCards.Values)
            {
                result.AddRange(cardsOfPlayer.Values);
            }
            return result;
        }

        public IEnumerable<int> AllTrackedCardsExcept(Faction f, int cardNumber)
        {
            var result = new List<int>();
            foreach (var kvp in TrackedTreacheryCards)
            {
                var cardsOfPlayer = kvp.Value;
                if (kvp.Key != f)
                {
                    result.AddRange(cardsOfPlayer.Values);
                }
                else
                {
                    result.AddRange(cardsOfPlayer.Where(c => c.Key != cardNumber).Select(c => c.Value));
                }
            }
            return result;
        }

        public int GetSelectedTraitor(Faction f, int nr)
        {
            var key = new Tuple<Faction, int>(f, nr);
            if (trackedTraitors.ContainsKey(key))
            {
                return trackedTraitors[key];
            }
            else
            {
                return -1;
            }
        }

        public void ChangeSelectedTraitor(Faction f, int cardNumber, int? leaderID)
        {
            var key = new Tuple<Faction, int>(f, cardNumber);
            if (trackedTraitors.ContainsKey(key))
            {
                trackedTraitors.Remove(key);
            }

            if (leaderID != null)
            {
                trackedTraitors.Add(key, (int)leaderID);
            }
        }

        public int GetDiscardedTraitor(int nr)
        {
            if (trackedDiscardedTraitors.ContainsKey(nr))
            {
                return trackedDiscardedTraitors[nr];
            }
            else
            {
                return -1;
            }
        }

        public void ChangeDiscardedTraitor(int nr, int? leaderID)
        {
            if (trackedDiscardedTraitors.ContainsKey(nr))
            {
                trackedDiscardedTraitors.Remove(nr);
            }

            if (leaderID != null)
            {
                trackedDiscardedTraitors.Add(nr, (int)leaderID);
            }
        }

        public void Discard(Faction f, int cardNumber)
        {
            var current = TreacheryCardManager.Lookup.Find(TrackedTreacheryCards[f][cardNumber]);
            TrackedTreacheryCards[f][cardNumber] = TreacheryCard.NONE;

            if (current.Type == TreacheryCardType.Metheor)
            {
                removedCards.Add(current.Id);
            }
            else
            {
                discardedCards.Add(current.Id);
            }
        }

        public void SetNotDiscarded(TreacheryCard card)
        {
            if (card.Type == TreacheryCardType.Metheor)
            {
                removedCards.Remove(card.Id);
            }
            else
            {
                discardedCards.Remove(card.Id);
            }
        }

        public void ClearDiscarded()
        {
            discardedCards.Clear();
        }

        public static void Write(ref string target, object item)
        {
            if (item == null)
            {
                target += ';';
            }
            else
            {
                target = target + item.ToString() + ';';
            }
        }

        public override string ToString()
        {
            string result = "";

            //Treachery cards
            foreach (var faction in TrackedTreacheryCards.Keys)
            {
                foreach (var cardnr in TrackedTreacheryCards[faction].Keys)
                {
                    Write(ref result, TrackedTreacheryCards[faction][cardnr]);
                }
            }

            //Traitors
            Write(ref result, trackedTraitors.Keys.Count);
            foreach (var key in trackedTraitors.Keys)
            {
                Write(ref result, key.Item1);
                Write(ref result, key.Item2);
                Write(ref result, trackedTraitors[key]);
            }

            //Discarded traitors
            Write(ref result, trackedDiscardedTraitors.Keys.Count);
            foreach (var key in trackedDiscardedTraitors.Keys)
            {
                Write(ref result, key);
                Write(ref result, trackedDiscardedTraitors[key]);
            }

            //Spice blow card
            Write(ref result, TrackedSpiceCard);

            //Discarded treachery cards
            Write(ref result, discardedCards.Count);
            foreach (var card in discardedCards)
            {
                Write(ref result, card);
            }

            return result;
        }

        public static GreenIntelligence Parse(Game g, string data)
        {
            var elts = data.Split(';');
            int elt = 0;

            try
            {
                var result = new GreenIntelligence(g);

                //Treachery cards
                foreach (var faction in result.TrackedTreacheryCards.Keys.ToList())
                {
                    foreach (var cardNumber in result.TrackedTreacheryCards[faction].Keys.ToList())
                    {
                        int cardID = int.Parse(elts[elt++]);
                        result.TrackedTreacheryCards[faction][cardNumber] = cardID;
                    }
                }

                //Traitors
                int count = int.Parse(elts[elt++]);
                for (int i = 0; i < count; i++)
                {
                    var faction = Enum.Parse<Faction>(elts[elt++]);
                    int cardNumber = int.Parse(elts[elt++]);
                    var traitorID = int.Parse(elts[elt++]);
                    result.ChangeSelectedTraitor(faction, cardNumber, traitorID);
                }

                //Discarded traitors
                count = int.Parse(elts[elt++]);
                for (int i = 0; i < count; i++)
                {
                    result.ChangeDiscardedTraitor(int.Parse(elts[elt++]), int.Parse(elts[elt++]));
                }

                //Spice blow card
                result.TrackedSpiceCard = elts[elt++];

                //Discarded treachery cards
                if (elt < elts.Length)
                {
                    count = int.Parse(elts[elt++]);
                    for (int i = 0; i < count; i++)
                    {
                        result.discardedCards.Add(int.Parse(elts[elt++]));
                    }
                }

                return result;
            }
            catch (Exception)
            {

            }

            return new GreenIntelligence(g);
        }
    }
}