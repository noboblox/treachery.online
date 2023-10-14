﻿/*
 * Copyright 2020-2023 Ronald Ossendrijver. All rights reserved.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Treachery.Shared
{
    public partial class Player : ICloneable
    {
        #region Construction

        public Player(Game game, string name)
        {
            Game = game;
            Name = name;
        }

        public Player(Game game, string name, Faction faction, bool isBot = false)
        {
            Game = game;
            Name = name;
            Faction = faction;
            IsBot = isBot;
        }

        #endregion Construction

        #region Properties

        public string Name { get; set; }

        private Faction _faction = Faction.None;
        public Faction Faction
        {
            get
            {
                return _faction;
            }
            set
            {
                _faction = value;
                Param = BotParameters.GetDefaultParameters(value);
            }
        }

        public int PositionAtTable { get; set; } = -1;

        public int Resources { get; set; }

        public int Extortion { get; set; }

        public int Bribes { get; set; }

        public int ResourcesAfterBidding { get; set; }

        public int BankedResources { get; set; }

        public int TransferrableResources { get; set; }

        public List<TreacheryCard> TreacheryCards { get; set; } = new();

        public List<TreacheryCard> KnownCards { get; set; } = new();

        public List<IHero> Traitors { get; set; } = new();

        public List<IHero> RevealedTraitors { get; set; } = new();

        public List<IHero> ToldTraitors { get; set; } = new();

        public List<IHero> ToldNonTraitors { get; set; } = new();

        public List<IHero> KnownNonTraitors { get; set; } = new();

        public List<IHero> DiscardedTraitors { get; set; } = new();

        public List<IHero> FaceDancers { get; set; } = new();

        public List<IHero> RevealedDancers { get; set; } = new();

        public List<IHero> ToldFacedancers { get; set; } = new();

        public List<IHero> ToldNonFacedancers { get; set; } = new();

        public List<Leader> Leaders { get; set; } = new();

        public int ForcesInReserve => Homeworlds.Sum(w => ForcesIn(w));

        public int SpecialForcesInReserve => Homeworlds.Sum(w => SpecialForcesIn(w));

        public int AnyForcesInReserves => ForcesInReserve + SpecialForcesInReserve;

        public Dictionary<Location, Battalion> ForcesInLocations { get; set; } = new();

        public Dictionary<Location, Battalion> ForcesOnPlanet => ForcesInLocations.Where(kvp => kvp.Key is not Homeworld).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        public IEnumerable<Battalion> BattalionsOnPlanet => ForcesInLocations.Where(kvp => kvp.Key is not Homeworld).Select(kvp => kvp.Value);

        public List<Ambassador> Ambassadors { get; set; } = new();

        public Faction PredictedFaction { get; set; }

        public int PredictedTurn { get; set; }

        public int ForcesKilled { get; set; }

        public int SpecialForcesKilled { get; set; }

        public int TotalForcesKilledInBattle { get; set; }

        public Faction Ally { get; set; }

        public bool SpecialKarmaPowerUsed { get; set; }

        public List<TechToken> TechTokens { get; private set; } = new();

        public bool NoFieldIsActive => Faction == Faction.White && ForcesInLocations.Any(locationWithForces => locationWithForces.Value.AmountOfSpecialForces > 0);

        public Leader MostRecentlyRevivedLeader { get; set; }

        public List<LeaderSkill> SkillsToChooseFrom { get; set; } = new();

        public List<Homeworld> Homeworlds { get; set; } = new();

        public Faction Nexus { get; set; } = Faction.None;

        protected Game Game { get; set; }

        #endregion Properties

        #region Forces

        private Battalion GetAndCreateIfNeeded(Location location)
        {
            if (!ForcesInLocations.TryGetValue(location, out var result))
            {
                result = new Battalion(Faction, 0, 0, location);
                ForcesInLocations.Add(location, result);
            }

            return result;
        }

        private void ChangeForces(Location location, int nrOfForces)
        {
            var result = GetAndCreateIfNeeded(location);
            result.ChangeForces(nrOfForces);
            if (result.TotalAmountOfForces == 0) ForcesInLocations.Remove(location);
        }

        private void ChangeSpecialForces(Location location, int nrOfForces)
        {
            var result = GetAndCreateIfNeeded(location);
            result.ChangeSpecialForces(nrOfForces);
            if (result.TotalAmountOfForces == 0) ForcesInLocations.Remove(location);
            CheckIfRedStarThresholdWasPassed();
        }

        public void AddForces(Location location, int nrOfForces, bool fromReserves)
        {
            if (fromReserves)
            {
                var sourceWorld = Homeworlds.FirstOrDefault(w => w.IsHomeOfNormalForces);
                if (sourceWorld != null) MoveForces(sourceWorld, location, nrOfForces);
            }
            else
            {
                ChangeForces(location, nrOfForces);
            }
        }

        public void AddSpecialForces(Location location, int nrOfForces, bool fromReserves)
        {
            if (fromReserves)
            {
                var sourceWorld = Homeworlds.FirstOrDefault(w => w.IsHomeOfSpecialForces);
                if (sourceWorld != null) MoveSpecialForces(sourceWorld, location, nrOfForces);
            }
            else
            {
                ChangeSpecialForces(location, nrOfForces);
            }
        }

        public void AddForcesToReserves(int nrOfForces)
        {
            var sourceWorld = Homeworlds.FirstOrDefault(w => w.IsHomeOfNormalForces);
            if (sourceWorld != null) ChangeForces(sourceWorld, nrOfForces);
        }

        private bool RedStarHomeworldIsOnLowThreshold { get; set; }
        private void CheckIfRedStarThresholdWasPassed()
        {
            if (Faction == Faction.Red && Game.Applicable(Rule.RedSpecialForces))
            {
                var homeworld = Homeworlds.FirstOrDefault(hw => hw.World == World.RedStar);
                bool hasLessThanThreshold = homeworld != null && AnyForcesIn(homeworld) < homeworld.Threshold;

                if (hasLessThanThreshold && !RedStarHomeworldIsOnLowThreshold)
                {
                    RedStarHomeworldIsOnLowThreshold = true;
                }
                else if (!hasLessThanThreshold && RedStarHomeworldIsOnLowThreshold)
                {
                    RedStarHomeworldIsOnLowThreshold = false;
                }
            }
        }

        public void AddSpecialForcesToReserves(int nrOfForces)
        {
            var sourceWorld = Homeworlds.FirstOrDefault(w => w.IsHomeOfSpecialForces);
            if (sourceWorld != null) ChangeSpecialForces(sourceWorld, nrOfForces);
        }

        public void RemoveForces(Location location, int nrOfForces)
        {
            ChangeForces(location, -nrOfForces);
        }

        public void RemoveSpecialForces(Location location, int nrOfForces)
        {
            ChangeSpecialForces(location, -nrOfForces);
        }

        public void RemoveForcesFromReserves(int nrOfForces)
        {
            AddForcesToReserves(-nrOfForces);
        }

        public void RemoveSpecialForcesFromReserves(int nrOfForces)
        {
            AddSpecialForcesToReserves(-nrOfForces);
        }

        public int ForcesIn(Location location)
        {
            if (ForcesInLocations.TryGetValue(location, out Battalion battalion))
            {
                return battalion.AmountOfForces;
            }
            else
            {
                return 0;
            }
        }

        public int SpecialForcesIn(Location location)
        {
            if (ForcesInLocations.TryGetValue(location, out Battalion battalion))
            {
                return battalion.AmountOfSpecialForces;
            }
            else
            {
                return 0;
            }
        }

        public int AnyForcesIn(Location location)
        {
            if (ForcesInLocations.TryGetValue(location, out Battalion battalion))
            {
                return battalion.TotalAmountOfForces;
            }
            else
            {
                return 0;
            }
        }

        public int ForcesIn(Territory t)
        {
            return t.Locations.Sum(l => ForcesIn(l));
        }

        public int SpecialForcesIn(Territory t)
        {
            return t.Locations.Sum(l => SpecialForcesIn(l));
        }

        public int AnyForcesIn(Territory t)
        {
            return t.Locations.Sum(l => AnyForcesIn(l));
        }

        public void ForcesToReserves(Location location)
        {
            var battaltion = ForcesInLocations[location];

            AddForcesToReserves(battaltion.AmountOfForces);

            if (Faction == Faction.Blue)
            {
                AddForcesToReserves(battaltion.AmountOfSpecialForces);
            }
            else if (Faction != Faction.White)
            {
                AddSpecialForcesToReserves(battaltion.AmountOfSpecialForces);
            }

            ForcesInLocations.Remove(location);
            CheckIfRedStarThresholdWasPassed();
        }

        public void ForcesToReserves(Location location, int amount)
        {
            AddForcesToReserves(amount);
            ChangeForces(location, -amount);
        }

        public void SpecialForcesToReserves(Location location, int amount)
        {
            if (Faction == Faction.Blue)
            {
                AddForcesToReserves(amount);
            }
            else if (Faction != Faction.White)
            {
                AddSpecialForcesToReserves(amount);
            }

            ChangeSpecialForces(location, -amount);
        }

        public void ForcesToReserves(Territory t, int amount, bool special)
        {
            if (amount > 0)
            {
                int toRemoveInTotal = amount;
                foreach (var l in t.Locations.OrderBy(l => l.SpiceBlowAmount))
                {
                    int forcesIn = special ? SpecialForcesIn(l) : ForcesIn(l);
                    if (forcesIn > 0)
                    {
                        int toRemoveInThisLocation = Math.Min(forcesIn, toRemoveInTotal);

                        if (special && Faction != Faction.Blue)
                        {
                            AddSpecialForcesToReserves(toRemoveInTotal);
                        }
                        else if (!special || Faction != Faction.White)
                        {
                            AddForcesToReserves(toRemoveInTotal);
                        }

                        if (special)
                        {
                            ChangeSpecialForces(l, -toRemoveInTotal);
                        }
                        else
                        {
                            ChangeForces(l, -toRemoveInTotal);
                        }

                        toRemoveInTotal -= toRemoveInThisLocation;
                    }

                    if (toRemoveInTotal == 0) break;
                }
            }
        }

        public void ForcesToReserves(Territory t)
        {
            foreach (var l in t.Locations.Where(l => AnyForcesIn(l) > 0))
            {
                ForcesToReserves(l);
            }
        }

        public int KillAllForces(Location location, bool inBattle)
        {
            if (ForcesInLocations.TryGetValue(location, out Battalion battallion))
            {
                int killCount = battallion.AmountOfForces;
                ForcesKilled += killCount;

                int specialKillCount = battallion.AmountOfSpecialForces;
                if (Faction == Faction.Blue)
                {
                    ForcesKilled += specialKillCount;
                }
                else
                {
                    SpecialForcesKilled += specialKillCount;
                }

                ForcesInLocations.Remove(location);
                CheckIfRedStarThresholdWasPassed();

                if (inBattle)
                {
                    TotalForcesKilledInBattle += killCount + specialKillCount;
                }

                return killCount + specialKillCount;
            }

            return 0;
        }

        public void KillAllForces(Territory t, bool inBattle)
        {
            foreach (var l in t.Locations.Where(l => AnyForcesIn(l) > 0))
            {
                KillAllForces(l, inBattle);
            }
        }

        public int KillForces(Location location, int amountOfForces, int amountOfSpecialForces, bool inBattle)
        {
            ChangeForces(location, -amountOfForces);
            ChangeSpecialForces(location, -amountOfSpecialForces);
            
            ForcesKilled += amountOfForces;

            if (Faction == Faction.Blue)
            {
                ForcesKilled += amountOfSpecialForces;
            }
            else
            {
                SpecialForcesKilled += amountOfSpecialForces;
            }

            if (inBattle)
            {
                TotalForcesKilledInBattle += amountOfForces + amountOfSpecialForces;
            }

            return amountOfForces + amountOfSpecialForces;
        }

        public void KillForces(Territory t, int amount, bool special, bool inBattle)
        {
            int toKill = amount;
            foreach (var l in t.Locations.OrderBy(l => l.SpiceBlowAmount))
            {
                int forcesIn = special ? SpecialForcesIn(l) : ForcesIn(l);
                if (forcesIn > 0)
                {
                    int toBeKilled = Math.Min(forcesIn, toKill);
                    if (special)
                    {
                        KillForces(l, 0, toBeKilled, inBattle);
                    }
                    else
                    {
                        KillForces(l, toBeKilled, 0, inBattle);
                    }
                    toKill -= toBeKilled;
                }

                if (toKill == 0) break;
            }
        }

        public void ReviveForces(int amount)
        {
            ForcesKilled -= amount;
            AddForcesToReserves(amount);
        }

        public void ReviveSpecialForces(int amount)
        {
            SpecialForcesKilled -= amount;
            AddSpecialForcesToReserves(amount);
        }

        public void ShipForces(Location l, int amount)
        {
            AddForces(l, amount, true);
        }

        public void ShipSpecialForces(Location l, int amount)
        {
            if (Faction != Faction.White)
            {
                RemoveSpecialForcesFromReserves(amount);
            }

            ChangeSpecialForces(l, amount);
        }

        public void ShipAdvisors(Location l, int amount)
        {
            RemoveForcesFromReserves(amount);
            ChangeSpecialForces(l, amount);
        }

        public void MoveForces(Location from, Location to, int amount)
        {
            ChangeForces(from, -amount);
            ChangeForces(to, amount);
        }

        public void MoveSpecialForces(Location from, Location to, int amount)
        {
            ChangeSpecialForces(from, -amount);
            ChangeSpecialForces(to, amount);
        }

        public void FlipForces(Location l, bool asAdvisors)
        {
            if (Faction == Faction.Blue)
            {
                if (asAdvisors)
                {
                    int numberOfForces = ForcesIn(l);
                    ChangeForces(l, -numberOfForces);
                    ChangeSpecialForces(l, numberOfForces);
                }
                else
                {
                    int numberOfSpecialForces = SpecialForcesIn(l);
                    ChangeForces(l, numberOfSpecialForces);
                    ChangeSpecialForces(l, -numberOfSpecialForces);
                }
            }
        }

        public void FlipForces(Territory t, bool asAdvisors)
        {
            foreach (var l in t.Locations)
            {
                FlipForces(l, asAdvisors);
            }
        }

        public int OccupyingForces(Location l) => ForcesIn(l) + (Faction == Faction.Blue ? 0 : SpecialForcesIn(l));

        public bool Occupies(Location l) => OccupyingForces(l) > 0;

        public bool Occupies(Territory t) => t.Locations.Any(l => Occupies(l));

        public IEnumerable<Location> OccupiedLocations => Game.Map.Locations(true).Where(l => Occupies(l));

        public IEnumerable<Territory> OccupiedTerritories => Game.Map.Territories(true).Where(t => Occupies(t));

        public bool Controls(Game g, Location l, bool contestedStongholdsCountAsControlled)
        {
            if (contestedStongholdsCountAsControlled)
            {
                return Occupies(l);
            }
            else
            {
                return Occupies(l) && g.NrOfOccupantsExcludingFaction(l, Faction) == 0;
            }
        }

        public bool Controls(Game g, Territory t, bool contestedStongholdsCountAsOccupied)
        {
            if (contestedStongholdsCountAsOccupied)
            {
                return Occupies(t);
            }
            else
            {
                return Occupies(t) && g.NrOfOccupantsExcludingFaction(t, Faction) == 0;
            }
        }

        public IEnumerable<Territory> TerritoriesWithForces => Game.Map.Territories(true).Where(t => AnyForcesIn(t) > 0);

        public IEnumerable<Location> LocationsWithAnyForces => ForcesInLocations.Keys;

        public IEnumerable<Location> LocationsWithAnyForcesInTerritory(Territory t)
        {
            if (t == null)
            {
                return Array.Empty<Location>();
            }
            else
            {
                return t.Locations.Where(l => AnyForcesIn(l) > 0);
            }
        }

        public FactionForce Force
        {
            get
            {
                return Faction switch
                {
                    Faction.Green => FactionForce.Green,
                    Faction.Black => FactionForce.Black,
                    Faction.Yellow => FactionForce.Yellow,
                    Faction.Red => FactionForce.Red,
                    Faction.Orange => FactionForce.Orange,
                    Faction.Blue => FactionForce.Blue,
                    Faction.Grey => FactionForce.Grey,
                    Faction.Purple => FactionForce.Purple,
                    Faction.Brown => FactionForce.Brown,
                    Faction.White => FactionForce.White,
                    Faction.Pink => FactionForce.Pink,
                    Faction.Cyan => FactionForce.Cyan,
                    _ => FactionForce.None,
                };
            }
        }

        public FactionSpecialForce SpecialForce
        {
            get
            {
                return Faction switch
                {
                    Faction.Red => FactionSpecialForce.Red,
                    Faction.Yellow => FactionSpecialForce.Yellow,
                    Faction.Blue => FactionSpecialForce.Blue,
                    Faction.Grey => FactionSpecialForce.Grey,
                    Faction.White => FactionSpecialForce.White,
                    _ => FactionSpecialForce.None,
                };
            }
        }
        public bool HasSpecialForces
        {
            get
            {
                return (
                    Game.Applicable(Rule.YellowSpecialForces) && Is(Faction.Yellow) ||
                    Game.Applicable(Rule.RedSpecialForces) && Is(Faction.Red)) ||
                    Is(Faction.Grey);
            }
        }

        #endregion Forces

        #region Information

        public Player AlliedPlayer => Game.GetPlayer(Ally);

        public bool HasAlly => Ally != Faction.None;

        public bool Has(TreacheryCard card) => TreacheryCards.Contains(card);

        public bool Has(TreacheryCardType cardtype) => TreacheryCards.Any(c => c.Type == cardtype);

        public bool Is(Faction f) => Faction == f;

        public bool OrAllyIs(Faction f) => Faction == f || Ally == f;

        public int MaximumNumberOfCards
        {
            get
            {
                var occupierOfBrownHomeworld = Game.OccupierOf(World.Brown);
                int occupationBonus = occupierOfBrownHomeworld != null && (occupierOfBrownHomeworld == this || occupierOfBrownHomeworld.Faction == Ally) ? 1 : 0;
                int atomicsPenalty = Game.AtomicsAftermath != null && (Faction == Faction.Cyan || Game.Version >= 158 && Ally == Faction.Cyan) ? 1 : 0;

                int amount = Faction switch
                {
                    Faction.Black => 8,
                    Faction.Brown => 5,
                    _ => 4
                };

                return amount + occupationBonus - atomicsPenalty;
            }
        }

        public bool HasRoomForCards => TreacheryCards.Count < MaximumNumberOfCards;

        public bool HandSizeExceeded => TreacheryCards.Count > MaximumNumberOfCards;

        public int NumberOfTraitors => Faction == Faction.Black ? 4 : 1;

        public int NumberOfFacedancers => Faction == Faction.Purple ? 3 : 0;

        public void AssignLeaders(Game g)
        {
            Leaders = Faction switch
            {
                Faction.Brown => LeaderManager.GetLeaders(Faction.Brown).Where(l => g.Applicable(Rule.BrownAuditor) || l.HeroType != HeroType.Auditor).ToList(),
                Faction.Pink => LeaderManager.GetLeaders(Faction.Pink).Where(l => l.HeroType != HeroType.Vidal).ToList(),
                _ => LeaderManager.GetLeaders(Faction).ToList(),
            };
        }

        public TreacheryCard Card(TreacheryCardType type) => TreacheryCards.FirstOrDefault(c => c.Type == type);

        public IEnumerable<IHero> UnrevealedTraitors => Traitors.Where(f => !RevealedTraitors.Contains(f));

        public IEnumerable<IHero> UnrevealedFaceDancers => FaceDancers.Where(f => !RevealedDancers.Contains(f));

        public bool MessiahAvailable => Game.Applicable(Rule.GreenMessiah) && Is(Faction.Green) && TotalForcesKilledInBattle >= 7 && Game.IsAlive(LeaderManager.Messiah);

        public bool HasKarma(Game g) => Karma.ValidKarmaCards(g, this).Any();

        public void InitializeHomeworld(Homeworld world, int initialNormalForces, int initialSpecialForces)
        {
            if (initialNormalForces > 0) AddForces(world, initialNormalForces, false);
            if (initialSpecialForces > 0) AddSpecialForces(world, initialSpecialForces, false);

            RedStarHomeworldIsOnLowThreshold = (world.World == World.RedStar && initialSpecialForces < world.Threshold);

            Homeworlds.Add(world);
        }

        public override string ToString()
        {
            return Name;
        }

        public bool HasHighThreshold(World w)
        {
            if (!Game.Applicable(Rule.Homeworlds)) return false;

            var homeworld = Homeworlds.FirstOrDefault(hw => hw.World == w);

            if (homeworld.World == World.RedStar) return !RedStarHomeworldIsOnLowThreshold;

            return homeworld != null && AnyForcesIn(homeworld) >= homeworld.Threshold;
        }

        public bool HasHighThreshold()
        {
            if (!Game.Applicable(Rule.Homeworlds)) return false;
            return Homeworlds.Any(w => HasHighThreshold(w.World));
        }

        public bool HasLowThreshold(World w)
        {
            if (!Game.Applicable(Rule.Homeworlds)) return false;

            var homeworld = Homeworlds.FirstOrDefault(hw => hw.World == w);

            if (homeworld.World == World.RedStar) return RedStarHomeworldIsOnLowThreshold;

            return homeworld != null && AnyForcesIn(homeworld) < homeworld.Threshold;
        }

        public bool HasLowThreshold()
        {
            if (!Game.Applicable(Rule.Homeworlds)) return false;
            return Homeworlds.Any(w => HasLowThreshold(w.World));
        }


        public bool Initiated(GameEvent e) => e != null && e.Initiator == Faction;

        public bool IsNative(Territory territory) => territory.Locations.Any(l => l is Homeworld hw && IsNative(hw));

        public bool IsNative(Homeworld hw) => Homeworlds.Contains(hw);

        public bool HaveForcesOnEachOthersHomeworlds(Player other) => ForcesInLocations.Keys.Any(l => other.Homeworlds.Contains(l)) || other.ForcesInLocations.Keys.Any(l => Homeworlds.Contains(l));

        public int GetHomeworldBattleContributionAndLasgunShieldLimit(Territory whereBattleHappens)
        {
            if (!whereBattleHappens.IsHomeworld || !IsNative(whereBattleHappens)) return 0;

            var homeworld = whereBattleHappens.Locations.First() as Homeworld;

            if (HasHighThreshold(homeworld.World))
            {
                return homeworld.BattleBonusAndLasgunShieldLimitAtHighThreshold;
            }
            else
            {
                return homeworld.BattleBonusAndLasgunShieldLimitAtLowThreshold;
            }

        }

        #endregion Information

        #region Support

        public object Clone()
        {
            var result = (Player)MemberwiseClone();

            result.TreacheryCards = new List<TreacheryCard>(TreacheryCards);
            result.Traitors = new List<IHero>(Traitors);
            result.FaceDancers = new List<IHero>(FaceDancers);
            result.RevealedDancers = new List<IHero>(RevealedDancers);
            result.Leaders = new List<Leader>(Leaders);
            result.ForcesInLocations = Utilities.CloneObjectDictionary(ForcesInLocations);
            result.TechTokens = new List<TechToken>(TechTokens);

            return result;
        }

        #endregion Support
    }
}