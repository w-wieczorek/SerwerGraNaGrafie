## Wstęp

Program służy do rozegrania gier turniejowych w grze *Krążki*.
Niniejszy opis zawiera zasady tej gry oraz protokół komunikacyjny, wg którego powinny działać programy klienckie.

Napisany jest w języku C# z wykorzystaniem [Avalonia UI](https://avaloniaui.net/), a jego kod źródłowy można podejrzeć na [githubie](https://github.com/w-wieczorek/SerwerKrazki).

## Uruchomienie serwera

Najpierw w katalogu, gdzie mamy program SerwerKrazki.exe tworzymy podkatalog Programy oraz plik gracze.jsonl. W katalogu Programy umieszczamy pliki wykonywalne (.exe) programów, które wezmą udział w turnieju. Następnie tworzymy plik gracze.jsonl wg poniższego schematu:

```json
{"Name": "Adam", "Surname": "Kowalski", "Program": "s12345.exe"}
{"Name": "Ewa", "Surname": "Gołąb", "Program": "s20006.exe"}
{"Name": "Bartłomiej", "Surname": "Kluska", "Program": "s32347.exe"}
{"Name": "Magdalena", "Surname": "Piekarska", "Program": "s42347.exe"}
{"Name": "Rafał", "Surname": "Żywczok", "Program": "s92399.exe"}
```

Dane te są automatycznie ładowane po włączeniu programu. Jeśli dane zawodników znajdują się w innym pliku, należy je załadować za pomocą
przycisku 'Wczytaj z pliku' znajdującego się w zakładce 'Gracze'.
Po tym można rozpocząć turniej odpowiednim przyciskiem w zakładce 'Turniej'. Serwer w oddzielnych wątkach będzie uruchamiał poszczególne pary programów i przekazywał ich komunikaty.

## Turniej

Turniej składa się z rund, których liczba wyliczana jest w zależności od liczby zawodników i preferencji uczestników. Rundy składają się z gier. Kolejne gry oraz rundy uruchamiane są automatycznie.
Turniej zostanie rozegrany systemem, który da się przedstawić na przykładzie z siedmioma zawodnikami i trzema rundami:

|z1|z2|z3|z4|z5|z6|z7|
|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
|2|3|4|5|6|7|1|
|3|4|5|6|7|1|2|
|4|5|6|7|1|2|3|

Numer zawodnika (z1, z2 itd.) losowany jest przed rozpoczęciem turnieju.

## Zasady gry

Rekwizytami są są białe i czarne krążki ułożone w dziesięciu stertach. Liczba krążków w stercie nie przekracza 40. Ruch polega na wybraniu jednego z krążków własnego koloru z dowolnej niepustej sterty, a następnie zdjęciu go wraz ze wszystkimi krążkami leżącymi nad nim. Zdjęte krążki nie uczestniczą już w dalszej grze. Przegrywa ten, kto nie może wykonać ruchu.

Na poniższym przykładzie mamy tylko dwie sterty, które posłużą do zilustrowania przebiegu gry oraz przyjętej notacji zapisywania ruchów. 

![rysunek](rysunek.png)

Taki stan początkowy byłby reprezentowany za pomocą łańcucha "|cbbcb|bcc|". Sterty numerujemy od zera, a ruch zapisujemy za pomocą pary numer stery oraz nowa pozycja, która tam wystąpiła po zagraniu ruchu. Na przykład, jeśli rozgrywkę rozpoczynają czarne, a ruch jaki wybrały polega na zdjęciu jednego czarnego krążka z drugiej sterty, to zapiszemy to jako "1 bc" (oznacza to stertę o indeksie 1, na której zostały dwa krążki, na spodzie biały, na górze czarny). Dalsza rozgrywka mogłaby przebiegać następująco: białe grają "0 cb", czarne grają "0" (po tym zagraniu na stercie pierwszej, o indeksie 0, nie ma już krążków), białe grają "1" i czarne nie mają możliwości wykonania ruchu, więc przegrywają.

## Protokół komunikacyjny

Protokół komunikacyjny oparty jest na odczytywaniu i zapisywaniu łańcuchów ze standardowego we/wy; każda komenda wysyłana z lub do serwera (za pośrednictwem cin i cout) powinna kończyć się znakiem nowej linii (endl). Używany w protokole stan początkowy krążków jest zgodny z losowym ich uporządkowaniem generowanym każdorazowo przed rozpoczęciem gry.

### Żądania klienta (wysyłają programy grające w turnieju)

```txt
210 [numer sterty] [stan sterty]   // Wyślij ruch, gdzie "numer stery" określa skąd zdejmujemy krążki.
```

Przykład:

```txt
210 7 bbbccbcbcbccb
```

### Odpowiedzi serwera

```txt
200 [opis gry]     // Rozpoczęcie gry; teraz Twoja kolej, wyślij komunikat 210.
220 [nr] [stan]    // Ruch wykonany przez przeciwnika, serwer oczekuje na Twój ruch.
230                // Wygrałeś wg zasad.
231                // Wygrałeś przez przekroczenie czasu (przeciwnika).
240                // Przegrałeś wg zasad.
241                // Przegrałeś przez przekroczenie czasu.
999 [opis]         // Błąd komendy, opis powinien wyjaśnić przyczynę.
```

Przykłady:

```txt
200 c |ccb|bcbc|bbbb|cbcccb|bbbccc|bcb|ccccb|bcbcbbccb||cbcbcccb|
220 3 cbccc
230
```

Opis gry składa się z dwóch części. Pierwsza część to znak ('b' lub 'c') informujący, jakim kolorem grasz (białe lub czarne). Druga część opisuje kolejność ułożenia krążków na poszczególnych stertach.

Limit czasowy na wysłanie swojego pierwszego i każdego kolejnego ruchu wynosi 200ms na ruch.

Po otrzymaniu odpowiedzi 230 lub wyższej, klient powinien zakończyć działanie.