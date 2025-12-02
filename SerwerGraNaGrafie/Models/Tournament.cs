using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SerwerGraNaGrafie.Models;

public class Tournament
{
    public DateTime Time { get; set; }
    public string? NumberOfRounds { get; set; }
    public List<string> Players { get; set; }
    public List<int> Points { get; set; }
}

public static class TournamentExtensions
{
    public static async Task SaveTournament(List<Player> players, string? liczbaRund)
    {
        Tournament ob = new Tournament();
        ob.Time = DateTime.Now;
        ob.NumberOfRounds = liczbaRund;
        ob.Players = new();
        foreach (var p in players)
        {
            ob.Players.Add($"{p.Name} {p.Surname}");
        }
        ob.Points = new();
        foreach (var p in players)
        {
            ob.Points.Add(p.Punkty);
        }
        string jsonLine = JsonSerializer.Serialize(ob);

        // Dodanie linii do pliku
        await using (var stream = new FileStream("wyniki.jsonl", FileMode.Append, FileAccess.Write))
        await using (var writer = new StreamWriter(stream, Encoding.UTF8))
        {
            await writer.WriteLineAsync(jsonLine);
        }
    }
}