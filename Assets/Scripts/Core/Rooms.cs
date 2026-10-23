// Где может быть фурри. Номера комнат — как на карте из дизайн-документа (docs/img/map.png).
public enum Room
{
    RestRoom,   // 4 · комната отдыха
    Hall,       // 3 · основной концертный зал
    Corridor2,  // 2 · коридор к правой двери
    Corridor5,  // 5 · коридор между комнатами и залом
    Server,     // 6 · серверная
    Feeding,    // 7 · кормушка
    Vent,       // 8 · проход и вентиляция
    PassageG,   // Г · проход к генератору
    DanceFloor, // коридор с дэнс полом перед офисом
    Door,       // за правой дверью офиса
    Generator,  // у генератора
}

public static class Rooms
{
    // Камеры на мониторе по порядку кнопок
    public static readonly Room[] Cameras =
        { Room.Corridor2, Room.Hall, Room.RestRoom, Room.Corridor5, Room.Server, Room.Feeding, Room.Vent, Room.PassageG };

    public static string CameraLabel(Room room)
    {
        switch (room)
        {
            case Room.Corridor2: return "2";
            case Room.Hall: return "3";
            case Room.RestRoom: return "4";
            case Room.Corridor5: return "5";
            case Room.Server: return "6";
            case Room.Feeding: return "7";
            case Room.Vent: return "8";
            case Room.PassageG: return "Г";
            default: return "";
        }
    }

    public static string Name(Room room)
    {
        switch (room)
        {
            case Room.RestRoom: return "Комната отдыха";
            case Room.Hall: return "Концертный зал";
            case Room.Corridor2: return "Коридор к двери";
            case Room.Corridor5: return "Коридор у зала";
            case Room.Server: return "Серверная";
            case Room.Feeding: return "Кормушка";
            case Room.Vent: return "Вентиляция";
            case Room.PassageG: return "Проход к генератору";
            case Room.DanceFloor: return "Дэнс пол";
            case Room.Door: return "За дверью";
            case Room.Generator: return "Генератор";
            default: return room.ToString();
        }
    }
}

// Что фурри видят и делают в офисе. В игре это NightDirector, в тестах и симуляции — свои версии.
public interface IOffice
{
    bool DoorClosed { get; }
    bool DanceFloorOn { get; }
    void StallGenerator();
    void DamageGenerator(float hp);
    void DrainFuel(float amount);
}
