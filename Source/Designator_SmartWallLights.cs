using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SmartWallLights;

public class Designator_SmartWallLights : Designator
{
    private const string WallLampDefName = "WallLamp";
    private const int MaxRoomCells = 1200;
    private const string LogPrefix = "[SmartWallLights]";

    private readonly ThingDef lightDef;
    private readonly ThingDef stuffDef;
    private readonly float glowRadius;
    private readonly int glowRadiusSquared;

    public override float Order => 2990.5f;

    public Designator_SmartWallLights() : this(DefaultWallLightDef(), null)
    {
    }

    public Designator_SmartWallLights(ThingDef lightDef, ThingDef stuffDef = null)
    {
        this.lightDef = lightDef;
        this.stuffDef = stuffDef;
        defaultLabel = "SmartWallLights.DesignatorLabel".Translate();
        defaultDesc = "SmartWallLights.DesignatorDesc".Translate();
        tutorTag = "SmartWallLights";
        useMouseIcon = true;

        if (lightDef != null)
        {
            icon = lightDef.uiIcon;
            iconAngle = lightDef.uiIconAngle;
            iconOffset = lightDef.uiIconOffset;
            glowRadius = lightDef.GetCompProperties<CompProperties_Glower>()?.glowRadius ?? 11f;
        }
        else
        {
            icon = BaseContent.BadTex;
            glowRadius = 11f;
        }

        glowRadiusSquared = Mathf.CeilToInt(glowRadius * glowRadius);
        soundSucceeded = SoundDefOf.Designate_PlaceBuilding;
    }

    public override AcceptanceReport CanDesignateCell(IntVec3 loc)
    {
        if (!IsSupportedWallLight(lightDef))
        {
            DebugLog("CanDesignateCell rejected: no supported wall light def.");
            return "SmartWallLights.NoWallLampDef".Translate();
        }

        if (!loc.InBounds(Map) || loc.Fogged(Map))
        {
            DebugLog($"CanDesignateCell rejected: cell={loc}, inBounds={loc.InBounds(Map)}, fogged={(loc.InBounds(Map) ? loc.Fogged(Map).ToString() : "n/a")}.");
            return false;
        }

        Room room = loc.GetRoom(Map);
        if (room == null || !room.ProperRoom || room.IsDoorway)
        {
            DebugLog($"CanDesignateCell rejected: cell={loc}, room={DescribeRoom(room)}.");
            return "SmartWallLights.NoRoom".Translate();
        }

        if (room.CellCount < 2 || room.CellCount > MaxRoomCells)
        {
            DebugLog($"CanDesignateCell rejected by size: cell={loc}, room={DescribeRoom(room)}, max={MaxRoomCells}.");
            return "SmartWallLights.RoomTooSmall".Translate();
        }

        DebugLog($"CanDesignateCell accepted: cell={loc}, room={DescribeRoom(room)}.");
        return AcceptanceReport.WasAccepted;
    }

    public override void DesignateSingleCell(IntVec3 c)
    {
        AcceptanceReport report = CanDesignateCell(c);
        if (!report.Accepted)
        {
            Messages.Message(report.Reason, MessageTypeDefOf.RejectInput, historical: false);
            return;
        }

        Room room = c.GetRoom(Map);
        DesignateRoom(room);
    }

    public bool DesignateFromWallLampPlacement(IntVec3 placementCell, Rot4 rotation)
    {
        DebugLog($"Shift wall light placement started: def={lightDef?.defName ?? "null"}, placementCell={placementCell}, rotation={DescribeRot(rotation)}, map={Map?.uniqueID.ToString() ?? "null"}.");

        if (!IsSupportedWallLight(lightDef))
        {
            DebugLog("Shift wall light placement rejected: no supported wall light def.");
            Messages.Message("SmartWallLights.NoWallLampDef".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }

        Room room = RoomFromWallLampPlacement(placementCell, rotation);
        if (room == null)
        {
            DebugLog($"Shift WallLamp placement rejected: no accepted room found for placementCell={placementCell}, rotation={DescribeRot(rotation)}.");
            Messages.Message("SmartWallLights.NoRoom".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }

        DebugLog($"Shift WallLamp placement resolved room: {DescribeRoom(room)}.");
        return DesignateRoom(room);
    }

    public List<LightPlacementPreview> PreviewFromWallLightPlacement(IntVec3 placementCell, Rot4 rotation)
    {
        if (!IsSupportedWallLight(lightDef))
        {
            return new List<LightPlacementPreview>();
        }

        Room room = RoomFromWallLampPlacement(placementCell, rotation);
        if (room == null || !RoomAccepted(room) || room.CellCount < 2 || room.CellCount > MaxRoomCells)
        {
            return new List<LightPlacementPreview>();
        }

        List<Candidate> chosen = PlannedCandidatesForRoom(room, out _);
        return chosen.Select(candidate => new LightPlacementPreview(candidate.Position, candidate.Rotation)).ToList();
    }

    private bool DesignateRoom(Room room)
    {
        DebugLog($"DesignateRoom started: room={DescribeRoom(room)}.");

        if (room == null || !RoomAccepted(room))
        {
            DebugLog($"DesignateRoom rejected: room={DescribeRoom(room)}.");
            Messages.Message("SmartWallLights.NoRoom".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }

        if (room.CellCount < 2 || room.CellCount > MaxRoomCells)
        {
            DebugLog($"DesignateRoom rejected by size: room={DescribeRoom(room)}, max={MaxRoomCells}.");
            Messages.Message("SmartWallLights.RoomTooSmall".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }

        List<IntVec3> roomCells = room.Cells.Where(cell => cell.InBounds(Map) && !cell.Impassable(Map)).ToList();
        DebugLog($"DesignateRoom cells: passableRoomCells={roomCells.Count}, totalRoomCells={room.CellCount}.");
        if (roomCells.Count < 2)
        {
            DebugLog($"DesignateRoom rejected: passableRoomCells={roomCells.Count}.");
            Messages.Message("SmartWallLights.RoomTooSmall".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }

        List<Candidate> chosen = PlannedCandidatesForRoom(room, out int remainingUncovered);
        DebugLog($"DesignateRoom chosen candidates: count={chosen.Count}, remainingUncovered={remainingUncovered}.");
        if (chosen.Count == 0)
        {
            DebugLog("DesignateRoom finished: nothing to place.");
            Messages.Message("SmartWallLights.NothingToPlace".Translate(), MessageTypeDefOf.NeutralEvent, historical: false);
            return false;
        }

        int placed = 0;
        foreach (Candidate candidate in chosen)
        {
            if (PlaceLamp(candidate))
            {
                placed++;
            }
        }

        if (placed == 0)
        {
            DebugLog("DesignateRoom rejected: chosen candidates placed=0 after final CanPlaceBlueprintAt checks.");
            Messages.Message("SmartWallLights.NoCandidates".Translate(), MessageTypeDefOf.RejectInput, historical: false);
            return false;
        }
        else if (remainingUncovered > 0)
        {
            DebugLog($"DesignateRoom partial success: placed={placed}, remainingUncovered={remainingUncovered}.");
            Messages.Message("SmartWallLights.Partial".Translate(placed, remainingUncovered), MessageTypeDefOf.CautionInput, historical: false);
        }
        else
        {
            DebugLog($"DesignateRoom success: placed={placed}.");
            Messages.Message("SmartWallLights.Placed".Translate(placed), MessageTypeDefOf.PositiveEvent, historical: false);
        }

        return true;
    }

    private List<Candidate> PlannedCandidatesForRoom(Room room, out int remainingUncovered)
    {
        List<IntVec3> roomCells = room.Cells.Where(cell => cell.InBounds(Map) && !cell.Impassable(Map)).ToList();
        List<Candidate> candidates = FindCandidates(roomCells);
        DebugLog($"DesignateRoom candidates: count={candidates.Count}.");
        if (candidates.Count == 0)
        {
            remainingUncovered = roomCells.Count;
            return new List<Candidate>();
        }

        HashSet<IntVec3> uncovered = new HashSet<IntVec3>(roomCells);
        RemoveAlreadyCoveredCells(uncovered, roomCells);
        DebugLog($"DesignateRoom coverage after existing lamps: uncovered={uncovered.Count}.");

        List<Candidate> chosen = ChooseCandidates(candidates, uncovered);
        remainingUncovered = uncovered.Count;
        return chosen;
    }

    public override void SelectedUpdate()
    {
        base.SelectedUpdate();
        IntVec3 mouseCell = UI.MouseCell();
        if (mouseCell.InBounds(Map))
        {
            Room room = mouseCell.GetRoom(Map);
            if (room != null && room.ProperRoom)
            {
                room.DrawFieldEdges();
            }
        }
    }

    private List<Candidate> FindCandidates(List<IntVec3> roomCells)
    {
        List<Candidate> candidates = new List<Candidate>();
        HashSet<string> seen = new HashSet<string>();
        int attachedPositions = 0;
        int blockedByCanPlace = 0;
        int noCoverage = 0;

        foreach (IntVec3 cell in roomCells)
        {
            foreach (Rot4 rotation in Rot4.AllRotations)
            {
                Thing wall = GenConstruct.GetWallAttachedTo(cell, rotation, Map);
                if (wall == null)
                {
                    continue;
                }

                attachedPositions++;
                string key = $"{cell.x},{cell.z},{rotation.AsInt}";
                if (!seen.Add(key))
                {
                    continue;
                }

                if (TooCloseToDoor(cell, wall.Position))
                {
                    blockedByCanPlace++;
                    continue;
                }

                if (!GenConstruct.CanPlaceBlueprintAt(lightDef, cell, rotation, Map, DebugSettings.godMode, null, null, stuffDef).Accepted)
                {
                    blockedByCanPlace++;
                    continue;
                }

                HashSet<IntVec3> coverage = CoverageFrom(cell, roomCells);
                if (coverage.Count > 0)
                {
                    candidates.Add(new Candidate(cell, rotation, wall.Position, coverage));
                }
                else
                {
                    noCoverage++;
                }
            }
        }

        DebugLog($"FindCandidates summary: roomCells={roomCells.Count}, attachedPositions={attachedPositions}, blockedByCanPlace={blockedByCanPlace}, noCoverage={noCoverage}, candidates={candidates.Count}.");
        return candidates;
    }

    private Room RoomFromWallLampPlacement(IntVec3 placementCell, Rot4 rotation)
    {
        Room placementRoom = placementCell.InBounds(Map) ? placementCell.GetRoom(Map) : null;
        DebugLog($"RoomFromWallLampPlacement: placementCell={placementCell}, placementRoom={DescribeRoom(placementRoom)}, accepted={RoomAccepted(placementRoom)}.");
        if (RoomAccepted(placementRoom))
        {
            return placementRoom;
        }

        Thing wall = GenConstruct.GetWallAttachedTo(placementCell, rotation, Map);
        if (wall == null)
        {
            DebugLog($"RoomFromWallLampPlacement: no attached wall for placementCell={placementCell}, rotation={DescribeRot(rotation)}.");
            return null;
        }

        DebugLog($"RoomFromWallLampPlacement: attachedWall={wall.def.defName} at {wall.Position}, rotation={DescribeRot(rotation)}.");
        Room bestRoom = null;
        int bestDistance = int.MaxValue;
        foreach (IntVec3 direction in GenAdj.CardinalDirections)
        {
            IntVec3 roomCell = wall.Position + direction;
            if (!roomCell.InBounds(Map) || roomCell == placementCell)
            {
                DebugLog($"RoomFromWallLampPlacement: skip adjacent cell={roomCell}, inBounds={roomCell.InBounds(Map)}, sameAsPlacement={roomCell == placementCell}.");
                continue;
            }

            Room room = roomCell.GetRoom(Map);
            DebugLog($"RoomFromWallLampPlacement: adjacent cell={roomCell}, direction={direction}, room={DescribeRoom(room)}, accepted={RoomAccepted(room)}.");
            if (!RoomAccepted(room))
            {
                continue;
            }

            int distance = (roomCell - placementCell).LengthHorizontalSquared;
            if (distance < bestDistance)
            {
                bestRoom = room;
                bestDistance = distance;
            }
        }

        return bestRoom;
    }

    private static bool RoomAccepted(Room room)
    {
        return room != null && room.ProperRoom && !room.IsDoorway;
    }

    private static void DebugLog(string message)
    {
        if (SmartWallLightsMod.Settings.debugLogging)
        {
            Log.Message($"{LogPrefix} {message}");
        }
    }

    private static string DescribeRoom(Room room)
    {
        if (room == null)
        {
            return "null";
        }

        return $"id={room.ID}, cells={room.CellCount}, proper={room.ProperRoom}, psychOutdoors={room.PsychologicallyOutdoors}, touchesEdge={room.TouchesMapEdge}, isDoorway={room.IsDoorway}, openRoof={room.OpenRoofCount}, regionCount={room.RegionCount}";
    }

    private static string DescribeRot(Rot4 rotation)
    {
        return $"{rotation}({rotation.AsInt})";
    }

    private void RemoveAlreadyCoveredCells(HashSet<IntVec3> uncovered, List<IntVec3> roomCells)
    {
        foreach (Thing thing in Map.listerThings.AllThings)
        {
            if (BuiltThingDef(thing.def) != lightDef || !thing.Spawned || thing.Position.GetRoom(Map) == null)
            {
                continue;
            }

            foreach (IntVec3 covered in CoverageFrom(thing.Position, roomCells))
            {
                uncovered.Remove(covered);
            }
        }
    }

    private List<Candidate> ChooseCandidates(List<Candidate> candidates, HashSet<IntVec3> uncovered)
    {
        if (SmartWallLightsMod.Settings.placementMode == PlacementMode.FullSymmetry)
        {
            return ChooseSymmetricCandidates(candidates, uncovered);
        }

        if (SmartWallLightsMod.Settings.placementMode == PlacementMode.Hybrid)
        {
            return ChooseHybridCandidates(candidates, uncovered);
        }

        List<Candidate> chosen = new List<Candidate>();
        AddSegmentCandidates(candidates, uncovered, chosen);
        return chosen;
    }

    private List<Candidate> ChooseHybridCandidates(List<Candidate> candidates, HashSet<IntVec3> uncovered)
    {
        List<Candidate> chosen = ChooseSymmetricCandidates(candidates, uncovered);
        AddSegmentCandidates(candidates, uncovered, chosen);
        return chosen;
    }

    private void AddSegmentCandidates(List<Candidate> candidates, HashSet<IntVec3> uncovered, List<Candidate> chosen)
    {
        HashSet<IntVec3> usedPositions = new HashSet<IntVec3>();
        foreach (Candidate candidate in chosen)
        {
            usedPositions.Add(candidate.Position);
        }

        List<List<Candidate>> segments = WallSegments(candidates);
        float idealSpacing = Mathf.Max(SmartWallLightsMod.Settings.minLampSpacing, glowRadius * 1.45f);

        foreach (List<Candidate> segment in segments)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(segment.Count / idealSpacing));
            List<Candidate> picks = CenteredPicks(segment, count);

            foreach (Candidate candidate in picks)
            {
                if (usedPositions.Contains(candidate.Position) || TooCloseToExistingLamp(candidate, chosen))
                {
                    continue;
                }

                if (CoverageGain(candidate, uncovered) == 0)
                {
                    continue;
                }

                chosen.Add(candidate);
                usedPositions.Add(candidate.Position);
                RemoveCoveredCells(uncovered, candidate);
            }
        }
    }

    private List<Candidate> ChooseSymmetricCandidates(List<Candidate> candidates, HashSet<IntVec3> uncovered)
    {
        List<Candidate> chosen = new List<Candidate>();
        HashSet<IntVec3> usedPositions = new HashSet<IntVec3>();
        Dictionary<string, Candidate> lookup = candidates.ToDictionary(SymmetryKey);
        List<List<Candidate>> segments = WallSegments(candidates);
        float idealSpacing = Mathf.Max(SmartWallLightsMod.Settings.minLampSpacing, glowRadius * 1.45f);

        AddDoorAnchoredSymmetryCandidates(segments, lookup, uncovered, chosen, usedPositions);

        foreach (List<Candidate> segment in segments)
        {
            int count = Mathf.Max(1, Mathf.CeilToInt(segment.Count / idealSpacing));
            foreach (Candidate candidate in CenteredPicks(segment, count))
            {
                TryAddSymmetricCandidatePair(candidate, lookup, uncovered, chosen, usedPositions);
            }
        }

        return chosen;
    }

    private void AddDoorAnchoredSymmetryCandidates(
        List<List<Candidate>> segments,
        Dictionary<string, Candidate> lookup,
        HashSet<IntVec3> uncovered,
        List<Candidate> chosen,
        HashSet<IntVec3> usedPositions)
    {
        foreach (IGrouping<string, List<Candidate>> group in segments.GroupBy(segment => SegmentGroupKey(segment[0])))
        {
            List<List<Candidate>> lineSegments = group.OrderBy(segment => SegmentAxis(segment[0])).ToList();
            for (int i = 0; i < lineSegments.Count - 1; i++)
            {
                Candidate beforeDoor = lineSegments[i][lineSegments[i].Count - 1];
                Candidate afterDoor = lineSegments[i + 1][0];
                if (!GapContainsDoor(beforeDoor, afterDoor))
                {
                    continue;
                }

                TryAddDoorAnchorSet(beforeDoor, afterDoor, lookup, uncovered, chosen, usedPositions);
            }
        }
    }

    private bool TryAddDoorAnchorSet(
        Candidate first,
        Candidate second,
        Dictionary<string, Candidate> lookup,
        HashSet<IntVec3> uncovered,
        List<Candidate> chosen,
        HashSet<IntVec3> usedPositions)
    {
        if (!TryGetOppositeCandidate(first, lookup, out Candidate firstOpposite) || !TryGetOppositeCandidate(second, lookup, out Candidate secondOpposite))
        {
            return false;
        }

        List<Candidate> set = new List<Candidate> { first, second, firstOpposite, secondOpposite }
            .GroupBy(candidate => candidate.Position)
            .Select(group => group.First())
            .ToList();

        if (set.Any(candidate => usedPositions.Contains(candidate.Position)))
        {
            return false;
        }

        if (set.Any(candidate => TooCloseToExistingLamp(candidate, chosen)))
        {
            return false;
        }

        if (set.Sum(candidate => CoverageGain(candidate, uncovered)) == 0)
        {
            return false;
        }

        foreach (Candidate candidate in set)
        {
            chosen.Add(candidate);
            usedPositions.Add(candidate.Position);
            RemoveCoveredCells(uncovered, candidate);
        }

        DebugLog($"Door anchored symmetry added: first={first.Position}, second={second.Position}, firstOpposite={firstOpposite.Position}, secondOpposite={secondOpposite.Position}.");
        return true;
    }

    private bool TryAddSymmetricCandidatePair(
        Candidate candidate,
        Dictionary<string, Candidate> lookup,
        HashSet<IntVec3> uncovered,
        List<Candidate> chosen,
        HashSet<IntVec3> usedPositions)
    {
        if (!TryGetOppositeCandidate(candidate, lookup, out Candidate opposite))
        {
            return false;
        }

        if (usedPositions.Contains(candidate.Position) || usedPositions.Contains(opposite.Position))
        {
            return false;
        }

        if (TooCloseToExistingLamp(candidate, chosen) || TooCloseToExistingLamp(opposite, chosen))
        {
            return false;
        }

        if (CoverageGain(candidate, uncovered) == 0 && CoverageGain(opposite, uncovered) == 0)
        {
            return false;
        }

        chosen.Add(candidate);
        chosen.Add(opposite);
        usedPositions.Add(candidate.Position);
        usedPositions.Add(opposite.Position);
        RemoveCoveredCells(uncovered, candidate);
        RemoveCoveredCells(uncovered, opposite);
        return true;
    }

    private bool GapContainsDoor(Candidate first, Candidate second)
    {
        if (first.Rotation != second.Rotation)
        {
            return false;
        }

        int firstAxis = SegmentAxis(first);
        int secondAxis = SegmentAxis(second);
        if (secondAxis <= firstAxis + 1)
        {
            return false;
        }

        for (int axis = firstAxis + 1; axis < secondAxis; axis++)
        {
            IntVec3 wallCell = first.Rotation == Rot4.North || first.Rotation == Rot4.South
                ? new IntVec3(axis, 0, first.WallPosition.z)
                : new IntVec3(first.WallPosition.x, 0, axis);

            if (wallCell.InBounds(Map) && CellHasDoorOrDoorBlueprint(wallCell))
            {
                return true;
            }
        }

        return false;
    }

    private bool PlaceLamp(Candidate candidate)
    {
        AcceptanceReport report = GenConstruct.CanPlaceBlueprintAt(lightDef, candidate.Position, candidate.Rotation, Map, DebugSettings.godMode, null, null, stuffDef);
        if (!report.Accepted)
        {
            DebugLog($"PlaceLamp rejected: cell={candidate.Position}, rotation={DescribeRot(candidate.Rotation)}, reason={report.Reason}.");
            return false;
        }

        if (DebugSettings.godMode)
        {
            Thing lamp = ThingMaker.MakeThing(lightDef, stuffDef);
            lamp.SetFactionDirect(Faction.OfPlayer);
            GenSpawn.Spawn(lamp, candidate.Position, Map, candidate.Rotation);
            DebugLog($"PlaceLamp god mode spawned: cell={candidate.Position}, rotation={DescribeRot(candidate.Rotation)}.");
            return true;
        }

        GenConstruct.PlaceBlueprintForBuild(lightDef, candidate.Position, Map, candidate.Rotation, Faction.OfPlayer, stuffDef);
        return true;
    }

    private static int CoverageGain(Candidate candidate, HashSet<IntVec3> uncovered)
    {
        int gain = 0;
        foreach (IntVec3 covered in candidate.Coverage)
        {
            if (uncovered.Contains(covered))
            {
                gain++;
            }
        }

        return gain;
    }

    private static void RemoveCoveredCells(HashSet<IntVec3> uncovered, Candidate candidate)
    {
        foreach (IntVec3 covered in candidate.Coverage)
        {
            uncovered.Remove(covered);
        }
    }

    private static List<List<Candidate>> WallSegments(List<Candidate> candidates)
    {
        List<List<Candidate>> segments = new List<List<Candidate>>();

        foreach (IGrouping<string, Candidate> group in candidates.GroupBy(SegmentGroupKey))
        {
            List<Candidate> sorted = group.OrderBy(SegmentAxis).ToList();
            List<Candidate> current = new List<Candidate>();
            int previous = int.MinValue;

            foreach (Candidate candidate in sorted)
            {
                int axis = SegmentAxis(candidate);
                if (current.Count > 0 && axis != previous + 1)
                {
                    segments.Add(current);
                    current = new List<Candidate>();
                }

                current.Add(candidate);
                previous = axis;
            }

            if (current.Count > 0)
            {
                segments.Add(current);
            }
        }

        segments.Sort((a, b) => b.Count.CompareTo(a.Count));
        return segments;
    }

    private static string SegmentGroupKey(Candidate candidate)
    {
        if (candidate.Rotation == Rot4.North || candidate.Rotation == Rot4.South)
        {
            return $"{candidate.Rotation.AsInt}:{candidate.Position.z}";
        }

        return $"{candidate.Rotation.AsInt}:{candidate.Position.x}";
    }

    private static int SegmentAxis(Candidate candidate)
    {
        if (candidate.Rotation == Rot4.North || candidate.Rotation == Rot4.South)
        {
            return candidate.Position.x;
        }

        return candidate.Position.z;
    }

    private static List<Candidate> CenteredPicks(List<Candidate> segment, int count)
    {
        List<Candidate> picks = new List<Candidate>();
        HashSet<int> takenIndexes = new HashSet<int>();

        for (int i = 0; i < count; i++)
        {
            float target = (i + 1) * (segment.Count - 1) / (float)(count + 1);
            int bestIndex = 0;
            float bestDistance = float.MaxValue;

            for (int index = 0; index < segment.Count; index++)
            {
                if (takenIndexes.Contains(index))
                {
                    continue;
                }

                float distance = Mathf.Abs(index - target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = index;
                }
            }

            takenIndexes.Add(bestIndex);
            picks.Add(segment[bestIndex]);
        }

        return picks;
    }

    private static bool TooCloseToExistingLamp(Candidate candidate, List<Candidate> chosen)
    {
        int minSpacing = SmartWallLightsMod.Settings.minLampSpacing;
        foreach (Candidate existing in chosen)
        {
            if ((existing.Position - candidate.Position).LengthHorizontalSquared < minSpacing * minSpacing)
            {
                return true;
            }
        }

        return false;
    }

    private bool TooCloseToDoor(IntVec3 lampCell, IntVec3 wallCell)
    {
        int distance = SmartWallLightsMod.Settings.doorAvoidanceDistance;
        if (distance <= 0)
        {
            return false;
        }

        CellRect rect = CellRect.FromLimits(
            Mathf.Min(lampCell.x, wallCell.x) - distance,
            Mathf.Min(lampCell.z, wallCell.z) - distance,
            Mathf.Max(lampCell.x, wallCell.x) + distance,
            Mathf.Max(lampCell.z, wallCell.z) + distance);
        rect.ClipInsideMap(Map);

        foreach (IntVec3 cell in rect)
        {
            if (CellHasDoorOrDoorBlueprint(cell))
            {
                return true;
            }
        }

        return false;
    }

    private bool CellHasDoorOrDoorBlueprint(IntVec3 cell)
    {
        if (cell.GetDoor(Map) != null)
        {
            return true;
        }

        List<Thing> things = cell.GetThingList(Map);
        for (int i = 0; i < things.Count; i++)
        {
            ThingDef builtDef = BuiltThingDef(things[i].def);
            if (builtDef != null && builtDef.IsDoor)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetOppositeCandidate(Candidate candidate, Dictionary<string, Candidate> lookup, out Candidate opposite)
    {
        Rot4 oppositeRotation = candidate.Rotation.Opposite;
        IntVec3 oppositeWall = candidate.WallPosition;

        if (candidate.Rotation == Rot4.North || candidate.Rotation == Rot4.South)
        {
            opposite = lookup.Values
                .Where(other => other.Rotation == oppositeRotation && other.Position.x == candidate.Position.x)
                .OrderByDescending(other => Mathf.Abs(other.Position.z - candidate.Position.z))
                .FirstOrDefault();
        }
        else
        {
            opposite = lookup.Values
                .Where(other => other.Rotation == oppositeRotation && other.Position.z == candidate.Position.z)
                .OrderByDescending(other => Mathf.Abs(other.Position.x - candidate.Position.x))
                .FirstOrDefault();
        }

        return opposite != null && opposite.Position != candidate.Position && opposite.WallPosition != oppositeWall;
    }

    private static string SymmetryKey(Candidate candidate)
    {
        return $"{candidate.Position.x},{candidate.Position.z},{candidate.Rotation.AsInt}";
    }


    private HashSet<IntVec3> CoverageFrom(IntVec3 source, List<IntVec3> roomCells)
    {
        HashSet<IntVec3> covered = new HashSet<IntVec3>();
        foreach (IntVec3 target in roomCells)
        {
            if ((target - source).LengthHorizontalSquared > glowRadiusSquared)
            {
                continue;
            }

            if (GenSight.LineOfSight(source, target, Map, skipFirstCell: true))
            {
                covered.Add(target);
            }
        }

        return covered;
    }

    private static ThingDef BuiltThingDef(ThingDef def)
    {
        return GenConstruct.BuiltDefOf(def) as ThingDef;
    }

    public static bool IsSupportedWallLight(ThingDef def)
    {
        return def != null
            && def.GetCompProperties<CompProperties_Glower>() != null
            && def.building != null
            && def.building.isAttachment
            && def.blueprintDef != null;
    }

    private static ThingDef DefaultWallLightDef()
    {
        ThingDef wallLamp = DefDatabase<ThingDef>.GetNamedSilentFail(WallLampDefName);
        if (IsSupportedWallLight(wallLamp))
        {
            return wallLamp;
        }

        return DefDatabase<ThingDef>.AllDefsListForReading.FirstOrDefault(IsSupportedWallLight);
    }

    public readonly struct LightPlacementPreview
    {
        public readonly IntVec3 Position;
        public readonly Rot4 Rotation;

        public LightPlacementPreview(IntVec3 position, Rot4 rotation)
        {
            Position = position;
            Rotation = rotation;
        }
    }

    private sealed class Candidate
    {
        public readonly IntVec3 Position;
        public readonly Rot4 Rotation;
        public readonly IntVec3 WallPosition;
        public readonly HashSet<IntVec3> Coverage;

        public Candidate(IntVec3 position, Rot4 rotation, IntVec3 wallPosition, HashSet<IntVec3> coverage)
        {
            Position = position;
            Rotation = rotation;
            WallPosition = wallPosition;
            Coverage = coverage;
        }
    }
}
