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

    private bool ZapiszRuch(string? message, bool[] aktywne, int[,] macierz, char color)
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
        if (nr < 0 || nr > 399 || !aktywne[nr] || (color == 'p' && nr % 2 != 0) || (color == 'n' && nr % 2 == 0))
        {
            return false;
        }
        for (int i = 0; i < 400; ++i)
        {
            if (aktywne[i] && macierz[nr, i] == 1)
            {
                aktywne[i] = false;
            }
        }
        aktywne[nr] = false;
        return true;
    } 
    
    private void GenerujLosowyGraf(bool[] aktywne, int[,] macierz)
    {
        Random rand = new Random();
        int n = 400;
        int docelowaLiczbaKrawedzi = 500;
        
        // Wyczyść macierz i aktywuj wszystkie wierzchołki
        for (int i = 0; i < n; i++)
        {
            aktywne[i] = true;
            for (int j = 0; j < n; j++)
            {
                macierz[i, j] = 0;
            }
        }
        
        // Tablica do śledzenia stopni wierzchołków
        int[] stopnie = new int[n];
        int liczbaKrawedzi = 0;
        
        while (liczbaKrawedzi < docelowaLiczbaKrawedzi)
        {
            // Wybierz pierwszy wierzchołek z wagami
            int v1 = WybierzWierzcholekZWagami(stopnie, rand);
            
            // Wybierz drugi wierzchołek z wagami (różny od pierwszego)
            int v2;
            int proby = 0;
            do
            {
                v2 = WybierzWierzcholekZWagami(stopnie, rand);
                proby++;
                // Zabezpieczenie przed nieskończoną pętlą
                if (proby > 1000)
                {
                    v2 = rand.Next(n);
                    while (v2 == v1 || macierz[v1, v2] == 1)
                    {
                        v2 = rand.Next(n);
                    }
                    break;
                }
            } while (v2 == v1 || macierz[v1, v2] == 1); // Nie wybieraj tego samego ani istniejącej krawędzi
            
            // Dodaj krawędź (symetrycznie, bo graf nieskierowany)
            macierz[v1, v2] = 1;
            macierz[v2, v1] = 1;
            
            // Zaktualizuj stopnie
            stopnie[v1]++;
            stopnie[v2]++;
            
            liczbaKrawedzi++;
        }
    }

    private int WybierzWierzcholekZWagami(int[] stopnie, Random rand)
    {
        int n = stopnie.Length;
        
        // Oblicz wagi dla wszystkich wierzchołków: (d + 2) / (d*d + 1)
        double[] wagi = new double[n];
        double sumaWag = 0;
        
        for (int i = 0; i < n; i++)
        {
            int d = stopnie[i];
            wagi[i] = (d + 2.0) / (d * d + 1.0);
            sumaWag += wagi[i];
        }
        
        // Wybierz losowo na podstawie rozkładu prawdopodobieństwa
        double losowa = rand.NextDouble() * sumaWag;
        double akumulowana = 0;
        
        for (int i = 0; i < n; i++)
        {
            akumulowana += wagi[i];
            if (losowa <= akumulowana)
            {
                return i;
            }
        }
        
        // Zabezpieczenie (nie powinno się zdarzyć)
        return n - 1;
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
        Func<char> plubn = () =>
        {
            return nxtRnd() < 0x80000000 ? 'p' : 'n';
        };
        var aktywne = new bool[400];
        int[,] macierz = new int[400, 400];
        Func<char, bool> canMove = c =>
        {
            int m = (c == 'p' ? 0 : 1);
            for (int i = 0; i < 400; ++i)
            {
                if (i % 2 == m && aktywne[i]) return true;
            }
            return false;
        };
        GenerujLosowyGraf(aktywne, macierz);
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
        char colorA = plubn();
        char colorB = colorA == 'p' ? 'n' : 'p';
        int nv = aktywne.Select(b => b ? 1 : 0).Sum();
        int ne = 0;
        for (int i = 0; i < 399; ++i)
        {
            for (int j = i + 1; j < 400; ++j)
            {
                if (macierz[i, j] == 1 && aktywne[i] && aktywne[j]) ++ne;
            }
        }
        initialLineForA += $"{nv} {ne} {colorA}";
        for (int i = 0; i < 400; ++i)
        {
            if (aktywne[i]) initialLineForA += $" {i}";
        }
        for (int i = 0; i < 399; ++i)
        {
            for (int j = i + 1; j < 400; ++j)
            {
                if (macierz[i, j] == 1 && aktywne[i] && aktywne[j])
                {
                    initialLineForA += $" {i} {j}";
                }
            }
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
                if (ZapiszRuch(message, aktywne, macierz, colorA))
                {
                    nv = aktywne.Select(b => b ? 1 : 0).Sum();
                    ne = 0;
                    for (int i = 0; i < 399; ++i)
                    {
                        for (int j = i + 1; j < 400; ++j)
                        {
                            if (macierz[i, j] == 1 && aktywne[i] && aktywne[j]) ++ne;
                        }
                    }
                    initialLineForB += $"{nv} {ne} {colorB}";
                    for (int i = 0; i < 400; ++i)
                    {
                        if (aktywne[i]) initialLineForB += $" {i}";
                    }
                    for (int i = 0; i < 399; ++i)
                    {
                        for (int j = i + 1; j < 400; ++j)
                        {
                            if (macierz[i, j] == 1 && aktywne[i] && aktywne[j])
                            {
                                initialLineForB += $" {i} {j}";
                            }
                        }
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
                    if (ZapiszRuch(message, aktywne, macierz, colorB))
                    {
                        if (canMove(colorA))
                        {
                            writerA.WriteLine(message!.Replace("210 ", "220 "));
                            Logs += $"Do z{idxA}: {message!.Replace("210 ", "220 ")}\n";
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
                                if (ZapiszRuch(message, aktywne, macierz, colorA))
                                {
                                    if (canMove(colorB))
                                    {
                                        writerB.WriteLine(message!.Replace("210 ", "220 "));
                                        Logs += $"Do z{idxB}: {message!.Replace("210 ", "220 ")}\n";
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