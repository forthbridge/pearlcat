using System.Collections.Generic;

namespace Pearlcat;

public class SaveMiscWorld
{
    // Meta
    public bool JustBeatAltEnd { get; set; }
    public bool JustMiraSkipped { get; set; }


    // Pearlcat
    public List<int> PlayersGivenPearls { get; } = new();
    public Dictionary<int, List<string>> Inventory { get; } = new();
    public Dictionary<int, int?> ActiveObjectIndex { get; } = new();
    public Dictionary<int, SpearModule> PearlSpears { get; } = new();


    // Pearlpup
    public int? PearlpupID { get; set; }
    public bool HasPearlpupWithPlayer { get; set; }
    public bool HasPearlpupWithPlayerDeadOrAlive { get; set; } = true;

    // Five Pebbles
    public int PebblesMeetCount { get; set; }
    public bool PebblesMetSickPup { get; set; }
    public Dictionary<int, int> PearlIDsBroughtToPebbles { get; } = new();
    public int UniquePearlsBroughtToPebbles => PearlIDsBroughtToPebbles.Keys.Count;
    public bool PebblesTookHalcyonPearl { get; set; } = false;
    

    // Looks to the Moon
    public int MoonSickPupMeetCount { get; set; }


    // Tutorial
    public bool ShownFullInventoryTutorial { get; set; }
    public bool ShownSpearCreationTutorial { get; set; }


    // Dreams
    public string? CurrentDream { get; set; }
    public List<string> PreviousDreams { get; } = new();



    // Chasing Wind
    public int CWMeetCount { get; set; }
    public int CWMeetSickCount { get; set; }
    public bool CWGavePearl { get; set; }
}
