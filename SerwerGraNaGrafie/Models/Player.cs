using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SerwerGraNaGrafie.Models;

public class Player
{
    public string Name { get; set; }
    public string Surname { get; set; }
    public string Program { get; set; }
    public int Punkty { get; set; }
}

public static class PlayerExtensions
{
    public static List<Player> ReadPlayersFromJson(string filePath)
    {
        List<Player> result = new();
        try
        {
            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var person = JsonSerializer.Deserialize<Player>(line);
                    result.Add(person);
                }
            }
        }
        catch (FileNotFoundException e)
        {
            result.Add(new() { Name = "", Surname = "", Program = "", Punkty = 0 });
        }
        return result;
    }
}
