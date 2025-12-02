using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SerwerGraNaGrafie.Models;


namespace SerwerGraNaGrafie.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private string _logs = "";
    [ObservableProperty] private string _tableOfPlayers = "";
    [ObservableProperty] private string _tableOfTournament = "";
    [ObservableProperty] private string _pomocMd = "";
    [ObservableProperty] private string? _liczbaRund;
    [ObservableProperty] private string? _timeLimit;
    [ObservableProperty] private bool _canChangePlayers = true;
    [ObservableProperty] private bool _tournamentIsFinished = false;
    [ObservableProperty] private bool _tournamentNotInProgress = true;
    private List<Player> _players;

    private void BuildHeadersForTournamentTable()
    {
        int i, j;
        TableOfTournament = "|z1|";
        j = 1;
        while (j < _players.Count)
        {
            TableOfTournament += $"z{++j}|";
        }
        TableOfTournament += "\n|:-|";
        for (i = 2; i <= j; i++)
        {
            TableOfTournament += ":-|";
        }
        TableOfTournament += "\n||";
    }
    
    private void BuildPlayersTableFromList()
    {
        int i = 1;
        TableOfPlayers = "|Nr|Imię|Nazwisko|Program|\n";
        TableOfPlayers += "|-:|:-|:-|:-|\n";
        foreach (Player p in _players)
        {
            TableOfPlayers += $"|{i++}|{p.Name}|{p.Surname}|{p.Program}|\n";
        }
        LiczbaRund = "1";
        BuildHeadersForTournamentTable();
    }
    
    public MainWindowViewModel()
    {
        _players = PlayerExtensions.ReadPlayersFromJson("gracze.jsonl");
        BuildPlayersTableFromList();
        PomocMd = File.ReadAllText("pomoc.md");
    }

    [RelayCommand]
    public async void OnWczytajZPliku()
    {
        TournamentIsFinished = false;
        var fileTypes = new List<FilePickerFileType>
        {
            new FilePickerFileType("JSONL Files")
            {
                Patterns = new[] { "*.jsonl" }
            },
            new FilePickerFileType("JSON Files")
            {
                Patterns = new[] { "*.json" }
            }
        };
        var files = await MyReferences.MainWindow.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions {
            Title = "Wybierz plik jsonl",
            FileTypeFilter = fileTypes,
            AllowMultiple = false
        });
        if (files.Count >= 1)
        {
            _players = PlayerExtensions.ReadPlayersFromJson(files[0].Name);
            BuildPlayersTableFromList();
        }
    }

    private void ShufflePlayers()
    {
        Random rng = new Random(); // Create a Random object for generating random numbers

        int n = _players.Count; // Get the number of players in the list

        // Fisher-Yates shuffle algorithm
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1); // Generate a random index between 0 and n (inclusive)
            
            // Swap the players at positions k and n
            (_players[k], _players[n]) = (_players[n], _players[k]);
        }
        BuildPlayersTableFromList();
    }

    private bool ZapiszRuch(string? message, ref string[] sterty, char color)
    {
        if (string.IsNullOrEmpty(message) || !message.StartsWith("210"))
        {
            return false;
        }
        string[] words = message.Split(' ');
        if (words.Length < 3)
        {
            words = words.Append("").ToArray();
        }
        int nr = int.Parse(words[1]);
        if (nr < 0 || nr > 9 || sterty[nr].Length <= words[2].Length)
        {
            return false;
        }
        if (sterty[nr].StartsWith(words[2]) && sterty[nr][words[2].Length] == color)
        {
            sterty[nr] = words[2];
            return true;
        }
        return false;
    }
    
    private char PlayGame(int idxA, int idxB)
    {
        int timeLimit = 300;
        if (TimeLimit is not null)
        {
            try
            {
                timeLimit = Int32.Parse(TimeLimit!);
            }
            catch (Exception ex) when (ex is FormatException || ex is FormatException)
            {
                timeLimit = 300;
            }
        }
        Logs = $"z{idxA} vs z{idxB}\n";
        char wynik = '?';
        uint rnd = (uint)DateTime.Now.GetHashCode();
        Func<uint> nxtRnd = () =>
        {
            rnd *= 1099087573;
            return rnd;
        };
        Func<char> blubc = () =>
        {
            return nxtRnd() < 0x80000000 ? 'b' : 'c';
        };
        var sterty = new string[10];
        Func<char, bool> canMove = c =>
        {
            return sterty.Any(s => s.Contains(c));
        };
        uint i, j, d;
        for (i = 0; i < 10; i++)
        {
            sterty[i] = i < 5 ? "b" : "c";
            d = nxtRnd() % 20 + 20;
            for (j = 0; j < d; j++)
            {
                sterty[i] += blubc();
            }
        }
        string Aname = _players[idxA - 1].Program;
        string Bname = _players[idxB - 1].Program;
        var processA = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(@".\Programy", Aname),
                RedirectStandardInput = true,  // Przekieruj stdin
                RedirectStandardOutput = true, // Przekieruj stdout
                UseShellExecute = false,      // Wyłącz shell, aby umożliwić przekierowanie
                CreateNoWindow = true         // Nie tworzyć okna konsoli
            }
        };   
        var processB = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(@".\Programy", Bname),
                RedirectStandardInput = true,  // Przekieruj stdin
                RedirectStandardOutput = true, // Przekieruj stdout
                UseShellExecute = false,      // Wyłącz shell, aby umożliwić przekierowanie
                CreateNoWindow = true         // Nie tworzyć okna konsoli
            }
        };
        string initialLineForA = "200 ";
        string initialLineForB = "200 ";
        char colorA = blubc();
        char colorB = colorA == 'c' ? 'b' : 'c';
        initialLineForA += colorA;
        initialLineForB += colorB;
        initialLineForB += " |";
        initialLineForA += " |";
        for (i = 0; i < 10; i++)
        {
            initialLineForA += $"{sterty[i]}|";
        }
        Stopwatch stopwatch = new Stopwatch();
        TimeSpan elapsedTime;
        // Uruchom oba procesy
        processA.Start();
        processB.Start();
        using (StreamReader readerA = processA.StandardOutput)
        using (StreamWriter writerB = processB.StandardInput)
        using (StreamReader readerB = processB.StandardOutput)
        using (StreamWriter writerA = processA.StandardInput)
        {
            int kod = 200;
            writerA.WriteLine(initialLineForA);
            Logs += $"Do z{idxA}: {initialLineForA}\n";
            stopwatch.Start();
            var message = readerA.ReadLine();
            stopwatch.Stop();
            Logs += $"Od z{idxA}: {message}\n";
            elapsedTime = stopwatch.Elapsed;
            if (elapsedTime.TotalMilliseconds > timeLimit)
            {
                wynik = '-';
                writerA.WriteLine("241");
                Logs += $"Do z{idxA}: 241\n";
                kod = 231;
                writerB.WriteLine("231");
                Logs += $"Do z{idxB}: 231\n";
            }
            else
            {
                if (ZapiszRuch(message, ref sterty, colorA))
                {
                    for (i = 0; i < 10; i++)
                    {
                        initialLineForB += $"{sterty[i]}|";
                    }
                    writerB.WriteLine(initialLineForB);
                    Logs += $"Do z{idxB}: {initialLineForB}\n";
                    stopwatch.Restart();
                }
                else
                {
                    wynik = '-';
                    writerA.WriteLine("999 Niepoprawny ruch");
                    Logs += $"Do z{idxA}: 999 Niepoprawny ruch\n";
                    kod = 230;
                    writerB.WriteLine("230");
                    Logs += $"Do z{idxB}: 230\n";
                }
            }
            while (kod < 230)
            {
                message = readerB.ReadLine();
                stopwatch.Stop();
                Logs += $"Od z{idxB}: {message}\n";
                elapsedTime = stopwatch.Elapsed;
                if (elapsedTime.TotalMilliseconds > timeLimit)
                {
                    wynik = '+';
                    writerB.WriteLine("241");
                    Logs += $"Do z{idxB}: 241\n";
                    kod = 231;
                    writerA.WriteLine("231");
                    Logs += $"Do z{idxA}: 231\n";
                }
                else
                {
                    if (ZapiszRuch(message, ref sterty, colorB))
                    {
                        if (canMove(colorA))
                        {
                            writerA.WriteLine(message!.Replace("210", "220"));
                            Logs += $"Do z{idxA}: {message!.Replace("210", "220")}\n";
                            kod = 220;
                            stopwatch.Restart();
                            message = readerA.ReadLine();
                            stopwatch.Stop();
                            Logs += $"Od z{idxA}: {message}\n";
                            elapsedTime = stopwatch.Elapsed;
                            if (elapsedTime.TotalMilliseconds > timeLimit)
                            {
                                wynik = '-';
                                writerA.WriteLine("241");
                                Logs += $"Do z{idxA}: 241\n";
                                kod = 231;
                                writerB.WriteLine("231");
                                Logs += $"Do z{idxB}: 231\n";
                            }
                            else
                            {
                                if (ZapiszRuch(message, ref sterty, colorA))
                                {
                                    if (canMove(colorB))
                                    {
                                        writerB.WriteLine(message!.Replace("210", "220"));
                                        Logs += $"Do z{idxB}: {message!.Replace("210", "220")}\n";
                                        kod = 220;
                                        stopwatch.Restart();
                                    }
                                    else
                                    {
                                        wynik = '+';
                                        writerB.WriteLine("240");
                                        Logs += $"Do z{idxB}: 240\n";
                                        writerA.WriteLine("230");
                                        Logs += $"Do z{idxA}: 230\n";
                                        kod = 230;
                                    }
                                }
                                else
                                {
                                    wynik = '-';
                                    writerA.WriteLine("999 Niepoprawny ruch");
                                    Logs += $"Do z{idxA}: 999 Niepoprawny ruch\n";
                                    kod = 230;
                                    writerB.WriteLine("230");
                                    Logs += $"Do z{idxB}: 230\n";
                                }
                            }
                        }
                        else
                        {
                            wynik = '-';
                            writerA.WriteLine("240");
                            Logs += $"Do z{idxA}: 240\n";
                            writerB.WriteLine("230");
                            Logs += $"Do z{idxB}: 230\n";
                            kod = 230;
                        }
                    }
                    else
                    {
                        wynik = '+';
                        writerB.WriteLine("999 Niepoprawny ruch");
                        Logs += $"Do z{idxB}: 999 Niepoprawny ruch\n";
                        kod = 230;
                        writerA.WriteLine("230");
                        Logs += $"Do z{idxA}: 230\n";
                    }
                }
            }
        }
        // Zamknij procesy
        processA.WaitForExit();
        processB.WaitForExit();
        return wynik;
    }

    private void PodsumujWyniki()
    {
        string[] lines = TableOfTournament.Split("\n");
        foreach (var p in _players)
        {
            p.Punkty = 0;
        }
        int nOfRounds = lines.Length - 3;
        Regex regex = new Regex(@"\|([+-])(\d+)");
        for (int i = 3; i <= nOfRounds + 2; i++)
        {
            MatchCollection matches = regex.Matches(lines[i]);
            int j = 0;
            foreach (Match match in matches)
            {
                if (match.Groups[1].Value == "+")
                {
                    _players[j].Punkty += 1;
                }
                else
                {
                    int przeciwnik = int.Parse(match.Groups[2].Value);
                    _players[przeciwnik - 1].Punkty += 1;
                }
                ++j;
            }
        }
        TableOfTournament += "\n||\n|";
        foreach (var p in _players)
        {
            TableOfTournament += $"**{p.Punkty}**|";
        }
    }
    
    [RelayCommand]
    public async Task OnRozpocznijTurniej()
    {
        await Task.Run(() =>
        {
            if (_players.Count > 1)
            {
                CanChangePlayers = false;
                TournamentIsFinished = false;
                TournamentNotInProgress = false;
                int nOfPlayers = _players.Count;
                int numberOfRounds = 1;
                try
                {
                    numberOfRounds = Int32.Parse(LiczbaRund);
                }
                catch (Exception)
                {
                    numberOfRounds = 1;
                }
                if (numberOfRounds <= 0)
                {
                    numberOfRounds = 1;
                }
                if (numberOfRounds >= nOfPlayers)
                {
                    numberOfRounds = nOfPlayers - 1;
                }
                ShufflePlayers();
                int currRound, playerA, playerB;
                for (currRound = 1; currRound <= numberOfRounds; currRound++)
                {
                    TableOfTournament += "\n|";
                    for (playerA = 1; playerA <= nOfPlayers; playerA++)
                    {
                        playerB = playerA + currRound;
                        if (playerB > nOfPlayers)
                        {
                            playerB -= nOfPlayers;
                        }

                        char wynik = PlayGame(playerA, playerB);
                        TableOfTournament += $"{wynik}{playerB}|";
                    }
                }
                PodsumujWyniki();
                CanChangePlayers = true;
                TournamentIsFinished = true;
                TournamentNotInProgress = true;
            }
        });
    }

    [RelayCommand]
    public async Task OnZapiszWyniki()
    {
        TournamentIsFinished = false;
        await TournamentExtensions.SaveTournament(_players, LiczbaRund);
    }
}