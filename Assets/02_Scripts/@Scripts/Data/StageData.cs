using System.Collections.Generic;

public class StageData : Data {
    public int Index { get; set; }
    public Difficulty Difficulty { get; set; }
    public List<ArrowData> Arrows { get; set; }
    
}

public enum Difficulty {
    Normal,
    Hard,
    VeryHard,
}

public enum ColorType {
    None = 0,
    Red = 2,
    Yellow = 3,
    Orange = 5,
    Purple = 6,
    Pink = 7,
    Sky = 9,
    Lime = 10,
    Black = 11,
}

public enum Direction {
    None = -1,
    Top,
    Right,
    Bottom,
    Left
}

public enum Orientation {
    None = -1,
    Vertical,
    Horizontal,
}