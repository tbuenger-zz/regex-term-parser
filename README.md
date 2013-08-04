Funktionsweise des FormelParsers
================================
Die fertig geparsete Formel ergibt einen Baum, bestehend aus Termen, von denen jeder Knoten entweder eine Variable, eine Konstante oder eine komplexere Operation ist, welche wiederum aus TeilTermen als Parametern besteht. (also wie immer)

Die Schwierigkeit beim Parsen ist bekanntlich das korrekte Entflechten der verschachtelten Strukturen (Klammerpaare) sowie das Erkennen der Operationen selbst, wobei auch Infix-Notationen möglich sein sollen.

Das Parsen läuft in zwei Schritten ab:

1. Vorverarbeitung
------------------
Die möglichen Infix-Operationen auf der höchsten Ebene werden in Präfix-Notation umgeformt: Also aus `14*x + cos(x/2)" wird "add(14, cos(x/2))`. Hierbei muss natürlich die Operator-Rangfolge beachtet werden, was durch die Reihenfolge des Schleifendurchlaufs aber bereits korrekt erfolgt.

2. Term-Erkennung (Operation / Variable / Konstante)
----------------------------------------------------
Da zuvor stets in Präfx-Notation umgeformt wurde, sehen die komplexen Operationen immer so aus `<name>(<param1>,<param2>,...)`. Dies wird erkannt, anhand des Namens die Operation ermittelt und jeder Parameter für sich wiederum geparset.
Wenn es keine komplexe Operation ist, könnte es eine Zahl sein, was uns Double.TryParse sagt.
Wenn es weder eine Operation, noch einer Zahl ist, könnte es eine Variable sein, falls es ein Wort mit mindestens einem `[a-z]` ist.
Sonst wird `0` angenommen. Dies ermöglicht bisher die unären Operationen `-x` und `+x`, da diese dann als `0-x` und `0+x` geparsed werden. Das ist aber noch nicht wirklich schön.
Funktionsweise der Regular Expressions:
Mit den RegEx-Erweiterungen des .NET-Frameworks lässt sich ziemlich zaubern.  cool 
Normalerweise können Reguläre Ausdrücke ja nur Wörter aus  Regulären Sprachen matchen. Ein bekanntes Beispiel, das keine reguläre Sprache ist, ist die Menge der Wörter

```{anbn} = {e, "ab", "aabb", "aaabbb", ...}```

Denn dies ist eine  kontextfreie und keine reguläre Sprache.
Das geht normalerweise mit regulären Ausdrücken nicht, aber in .NET schon!
Beginnen wir simpel mit dem RegEx `a*b*`
matched leider auch Wörter, die wir nicht wollen, da die Anzahl der as und bs nicht berücksichtigt wird. So gehts als nicht.

Aber schauen wir uns an, was .NET uns noch zur Verfügung stellt:

* **Unbenannte Gruppen**
`(subexpression)` ist eine Gruppierung innerhalb des RegEx, deren SubMatch später mit `Match.Groups[0...].Value erfragt werden kann.

* **Benannte Gruppen**
`(?<name>subexpression)` ist eine benannte Gruppe, die wiederum mit `Match.Groups["name"].Value` erfragt werden kann.
Zudem ist diese benannte Gruppe intern eigentlich ein Stack mit allen Treffern, die sie gefunden hat, was man auch über `Match.Groups["name"].Captures` ermitteln kann. Eine benannte Gruppe legt also den Teilmatch auch gleich auf den gleichnamigen Stack.

* **"negative" Gruppen**
`(?<-name>subexpression)` macht genau das umgekehrte, es entfern das oberste Element des "name"-Stacks, sofern der Teilausdruck matched. (deshalb dürfen Gruppennamen auch nicht mit `-` beginnen)

* **Positive und negative Lookarounds**
`(?=...)` und `(?!...)` sowie `(?<=...)` und `(?<!...)` sind positives/negative Look-Aheads bzw Look-Behinds. Sie überprüfen, ob der SubRegEx davor/danach auftritt/nicht auftritt, aber ohne ihn in den resultieren Match aufzunehmen.
Um beispielsweise in einem Quellcode alle Anführungszeichen zu finden (für Stringerkennung), die jedoch nicht esacped sein dürfen (also keine `\"`), lassen sich super negative Look-Behinds nutzen. So prüft `(?<!\\)` ob vorher kein Backslash auftrat. Somit gibt uns `(?<!\\)\"` einen Match bei jedem "echten" Anführungszeichen.

* **Leerer Regex**
Der leere Regex matched immer. (das leere Wort Epsilon, kann eben überall gefunden werden)
Somit würde ein positive Look-Ahead nach dem leeren Wort ebenfalls immer erfüllt sein. 
Somit ist `(?=)` immer erfüllt und könnte eigentlich weggelassen werden. Im Umkehrschluss bedeutet das, dass der negative Look-Ahead nach dem leeren Wort immer fehlschlägt, also `(?!)` nie erfüllt sein kann.

* **Alternativen**
`(?(name)yesSubexpression|noSubexpression)` ist so ein alternativer RegEx. Sie entspricht dem "yesSubexpression"-Teil, wenn die benannte Aufzeichnung eine Übereinstimmung hat. Andernfalls entspricht es dem "noSubexpression"-Teil. Oder in Stack-Sprech: je nachdem , ob der "name"-Stack leer ist oder nicht.

Nehmen wie also

`(?(stackname)a|b)`

, was je nachdem, ob der "stackname"-Stack Elemente enthält oder nicht nach einem "a" oder einem "b" sucht, so können wir diesen anpassen, dass er immer matched, wenn der Stack leer ist, aber nie matched, wenn der Stack elemente enthält.
Nämlich `(?(stackname)(?!)|(?=))`, oder vereinfacht, also ohne else-Teil `(?(stackname)(?!))`.
Und mit diesen Hilfsmitteln können wir per RegEx nach anbn suchen.
mit `(?<temp>a)+` packen wir bei jedem 'a' dieses auf den "temp"-Stack.
Mit `(?<-temp>b)+` entfernen wir bei jedem 'b' ein Element vom "temp"-Stack.
Und mit `(?(temp)(?!))` lassen wir den Match immer fehlschlagen, falls der Stack nicht-leer ist. (also unterschiedliche viele As und Bs vorhanden waren)

`(?<temp>a)+(?<-temp>b)+(?(temp)(?!))`

3. Komplettes Parsen
-----------------
Diese stack-orientierten Gruppen der regulären Ausdrücke sind der eigentliche Clou des Ganzen.

Für die Infix-Suffix-Umwandlung müssen wir beispielsweise das Operatorzeichen "+" zwischen korrekt geklammerten Ausdrücken suchen. Also in

`(34+x)+sin(4+x+1)`

wäre es das zweite Plus und eben nur dieses ! 
Das zweite Plus ist das gute, weil es das einzige ist, auf dessen linken Seite die Zahl der öffnenden und schließenden Klammern gleich ist. (Plusse in Sub-Ausdrücken haben links immer mehr öffnende als schließende Klammern)

Und danach können wir nun mit dem folgenden Regex suchen: (der lesbarkeit zeilenweise angegeben)

 Bedeutung              | Regex
:-----------------------|:-----
 Stringanfang           | `^` 
 linker Operand         | `((?:(?&lt;openBrackets&gt;\()&#124;(?&lt;-openBrackets&gt;\))&#124;[^\(\)])*)(?(openBrackets)(?!))`
 Whitespace             | `\s*`
 Operatorsymbol         | `\+`
 Whitespace             | `\s*`
 Rest (rechter Operand) | `(.*)`
 Stringende             | `$`


Noch zur Erläuterung des "linken Operanden":
Dieser besteht aus öffnenden Klammern, die auf den Stack gepackt werden: `(?<openBrackets>\()`
oder aus schließenden Klammern, die was vom Stack entfernen. `(?<-openBrackets>\))`
oder aus Nicht-Klammer-Zeichen. `[^\(\)]`
Und davon insgesamt beliebig viele. Allerdings muss der Klammernstack am Ende leer sein: `(?(openBrackets)(?!))`

(Der linke und rechte Operand sind nochmal in `(...)` gruppiert, damit ich auf diese einzeln zugreifen kann. So ergibt sich der Präfix-umgeformte Term schlicht aus `Regex.Replace("add($1,$2)")` mit Back-Referenzen `$1`,`$2` auf diese beiden Gruppen)
