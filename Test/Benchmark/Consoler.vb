Option Explicit On
Option Strict On
Option Infer On

Namespace Benchmark

    Friend NotInheritable Class ConsoleReporter

        Public Shared Sub PrintSummary(s As Summary)

            Console.OutputEncoding = System.Text.Encoding.UTF8

            Dim t As Table = TableFromSummary(s)
            t.Titles.AddRange({
                (ConsoleColor.White, $"Summary - {s.TestName} - {s.TestIterations} iterations x {s.ThreadsUsed} threads"),
                (ConsoleColor.Gray, s.TestDescription)
            })
            t.Print()

        End Sub

        ' ==========================================================
        ' Metrics helpers
        ' ==========================================================
        Private Shared Function TicksToMs(ticks As Long) As Double
            Return (ticks * 1000.0R) / Stopwatch.Frequency
        End Function

        Private Shared Function FormatTime(ms As Double) As String
            If ms < 1 Then
                Return $"{ms * 1000.0R:F2} µs"
            End If
            If ms < 10 Then Return $"{ms:F3} ms"
            If ms < 100 Then Return $"{ms:F2} ms"
            Return $"{ms:F1} ms"
        End Function

        Private Shared Function FormatBytes(bytesVal As Double) As String
            Dim b = Math.Max(0, bytesVal)
            If b < 1024 Then Return $"{b:F0} B"
            b /= 1024.0R
            If b < 1024 Then Return $"{b:F2} KB"
            b /= 1024.0R
            If b < 1024 Then Return $"{b:F2} MB"
            b /= 1024.0R
            Return $"{b:F2} GB"
        End Function

        Private Shared Function FormatDelta(baseMs As Double, ms As Double) As String
            If baseMs <= 0 OrElse ms <= 0 Then Return " n/a "
            Dim ratio As Double = baseMs / ms
            Return $"{ratio:F2}x"
        End Function

        Private Shared Function TableFromSummary(s As Summary) As Table

            Dim t As New Table()

            ' -------------------------
            ' Colunas (header/cell colors por coluna)
            ' -------------------------
            t.AddColumn("Actor",
                alignment:=Table.Enum_Alignment.Left,
                headerColor:=ConsoleColor.Cyan,
                cellColor:=ConsoleColor.Gray)

            t.AddColumn("Size (vs JSON)",
                alignment:=Table.Enum_Alignment.Right,
                headerColor:=ConsoleColor.Cyan,
                cellColor:=ConsoleColor.White)

            t.AddColumn("Encode",
                alignment:=Table.Enum_Alignment.Right,
                headerColor:=ConsoleColor.Cyan,
                cellColor:=ConsoleColor.White)

            t.AddColumn("Decode",
                alignment:=Table.Enum_Alignment.Right,
                headerColor:=ConsoleColor.Cyan,
                cellColor:=ConsoleColor.White)

            t.AddColumn("Total",
                alignment:=Table.Enum_Alignment.Right,
                headerColor:=ConsoleColor.Cyan,
                cellColor:=ConsoleColor.White)

            If s Is Nothing OrElse s.Snapshots Is Nothing OrElse s.Snapshots.Count = 0 Then
                t.AddRow("No data", "", "", "", "")
                Return t
            End If

            Dim list As New List(Of TestActor.Snapshot)
            For Each row In s.Snapshots
                list.Add(row.Value)
            Next

            ' Baseline: Newtonsoft (se não existir, usa o primeiro)
            Dim n As TestActor.Snapshot = list.FirstOrDefault(Function(x) String.Equals(x.ActorName, "Newtonsoft", StringComparison.OrdinalIgnoreCase))
            If n Is Nothing Then n = list(0)

            ' Bytery (para footer)
            Dim b As TestActor.Snapshot = list.FirstOrDefault(Function(x) String.Equals(x.ActorName, "Bytery", StringComparison.OrdinalIgnoreCase))

            ' GPT (novo agente)
            Dim g As TestActor.Snapshot = list.FirstOrDefault(Function(x) String.Equals(x.ActorName, "GPT", StringComparison.OrdinalIgnoreCase))

            ' Reordena: Newtonsoft, GPT, outros, Bytery (sempre último)
            Dim ordered As New List(Of TestActor.Snapshot)(list.Count)

            ' 1) Baseline primeiro (Newtonsoft se existir; senão o primeiro da lista)
            ordered.Add(n)

            ' 2) GPT logo abaixo do baseline (se existir e não for o baseline)
            If g IsNot Nothing AndAlso Not Object.ReferenceEquals(g, n) Then
                ordered.Add(g)
            End If

            ' 3) Todos os outros (exceto baseline/GPT/Bytery)
            For Each it In list
                If Object.ReferenceEquals(it, n) Then Continue For
                If g IsNot Nothing AndAlso Object.ReferenceEquals(it, g) Then Continue For
                If b IsNot Nothing AndAlso Object.ReferenceEquals(it, b) Then Continue For
                ordered.Add(it)
            Next

            ' 4) Bytery por último (se existir e não for o baseline)
            If b IsNot Nothing AndAlso Not Object.ReferenceEquals(b, n) Then
                ordered.Add(b)
            End If

            ' Baseline metrics
            Dim nEncMs As Double = TicksToMs(n.TotalEncodeTicks)
            Dim nDecMs As Double = TicksToMs(n.TotalDecodeTicks)
            Dim nTotMs As Double = nEncMs + nDecMs
            Dim nSize As Double = Math.Max(0, CDbl(n.EncodeBytesLength))

            Dim sizeCell = Function(sz As Double) As String
                               Return FormatBytes(sz)
                           End Function

            Dim timeCell = Function(ms As Double) As String
                               Return FormatTime(ms)
                           End Function

            ' -------------------------
            ' Rows
            ' -------------------------
            For Each snap In ordered

                Dim encMs As Double = TicksToMs(snap.TotalEncodeTicks)
                Dim decMs As Double = TicksToMs(snap.TotalDecodeTicks)
                Dim totMs As Double = encMs + decMs
                Dim sz As Double = Math.Max(0, CDbl(snap.EncodeBytesLength))

                Dim isBase As Boolean = Object.ReferenceEquals(snap, n)

                t.AddRow(
                    snap.ActorName,
                    sizeCell(sz),
                    timeCell(encMs),
                    timeCell(decMs),
                    timeCell(totMs)
                )

            Next

            ' -------------------------
            ' Footer: comparativo do Bytery
            ' -------------------------
            If b IsNot Nothing Then

                Dim bEncMs As Double = TicksToMs(b.TotalEncodeTicks)
                Dim bDecMs As Double = TicksToMs(b.TotalDecodeTicks)
                Dim bTotMs As Double = bEncMs + bDecMs
                Dim bSize As Double = Math.Max(0, CDbl(b.EncodeBytesLength))

                Dim sizePct As Double = If(nSize <= 0, 0, (bSize / nSize) * 100.0R)

                Dim encDelta As String = FormatDelta(nEncMs, bEncMs)
                Dim decDelta As String = FormatDelta(nDecMs, bDecMs)
                Dim totDelta As String = FormatDelta(nTotMs, bTotMs)

                ' Cor do footer (uma só): verde se Bytery melhor em total e size, amarelo se misto, vermelho se pior nos dois
                Dim fasterTotal As Boolean = (bTotMs > 0 AndAlso nTotMs > 0 AndAlso bTotMs < nTotMs)
                Dim smaller As Boolean = (nSize > 0 AndAlso bSize <= nSize)

                Dim footerCol As ConsoleColor = ConsoleColor.DarkYellow
                If fasterTotal AndAlso smaller Then footerCol = ConsoleColor.Green
                If (Not fasterTotal) AndAlso (Not smaller) Then footerCol = ConsoleColor.Red

                t.SetFooter(footerCol,
                    "Bytery vs JSON",
                    $"{sizePct:F1}%",
                    $"Enc {encDelta}",
                    $"Dec {decDelta}",
                    $"Tot {totDelta}"
                )

            End If

            Return t

        End Function

#Region "Printing Helpers"

        Private NotInheritable Class Table

            Public Enum Enum_Alignment
                Left
                Center
                Right
            End Enum

            Public Titles As New List(Of (color As ConsoleColor, text As String))

            Public ReadOnly Columns As New List(Of String)
            Public ReadOnly ColumnsAlignment As New List(Of Enum_Alignment)
            Public ReadOnly ColumnHeaderColors As New List(Of ConsoleColor)
            Public ReadOnly ColumnCellColor As New List(Of ConsoleColor)

            ' Cada linha é um array com o mesmo tamanho de ColumnCount
            Public ReadOnly Cells As New List(Of String())

            ' ---------
            ' Footer
            ' ---------
            Public Property FooterColor As ConsoleColor = ConsoleColor.DarkCyan
            Public Property FooterCells As String() = Nothing ' Nothing => sem footer

            Public ReadOnly Property HasFooter As Boolean
                Get
                    Return FooterCells IsNot Nothing
                End Get
            End Property

            Public ReadOnly Property ColumnCount As Integer
                Get
                    Return Columns.Count
                End Get
            End Property

            Public ReadOnly Property RowCount As Integer
                Get
                    Return Cells.Count
                End Get
            End Property

            Public Sub AddColumn(header As String,
                         Optional alignment As Enum_Alignment = Enum_Alignment.Left,
                         Optional headerColor As ConsoleColor = ConsoleColor.Green,
                         Optional cellColor As ConsoleColor = ConsoleColor.White)

                Columns.Add(If(header, ""))
                ColumnsAlignment.Add(alignment)
                ColumnHeaderColors.Add(headerColor)
                ColumnCellColor.Add(cellColor)

                ' Garante que linhas já existentes continuem compatíveis
                For i As Integer = 0 To Cells.Count - 1
                    Cells(i) = NormalizeRow(Cells(i))
                Next

                ' Garante que o footer (se existir) continue compatível
                If FooterCells IsNot Nothing Then
                    FooterCells = NormalizeRow(FooterCells)
                End If

            End Sub

            Public Sub AddRow(ParamArray values As String())
                If ColumnCount = 0 Then Throw New InvalidOperationException("Add at least 1 column before adding rows.")
                Cells.Add(NormalizeRow(values))
            End Sub

            Public Sub SetFooter(ParamArray values As String())
                SetFooter(FooterColor, values)
            End Sub

            Public Sub SetFooter(color As ConsoleColor, ParamArray values As String())
                If ColumnCount = 0 Then Throw New InvalidOperationException("Add at least 1 column before setting footer.")
                FooterColor = color
                FooterCells = NormalizeRow(values)
            End Sub

            Public Sub ClearFooter()
                FooterCells = Nothing
            End Sub

            Public Function ColumnWidth(index As Integer) As Integer
                If index < 0 OrElse index >= ColumnCount Then Throw New ArgumentOutOfRangeException(NameOf(index))

                Dim width As Integer = If(Columns(index), "").Length

                For Each row As String() In Cells
                    If row Is Nothing OrElse row.Length <= index Then Continue For
                    Dim cellText As String = If(row(index), "")
                    width = Math.Max(width, cellText.Length)
                Next

                If FooterCells IsNot Nothing AndAlso FooterCells.Length > index Then
                    Dim ft As String = If(FooterCells(index), "")
                    width = Math.Max(width, ft.Length)
                End If

                Return width
            End Function

            Public Function ColumnWidths() As Integer()
                Dim w(ColumnCount - 1) As Integer
                For i As Integer = 0 To ColumnCount - 1
                    w(i) = ColumnWidth(i)
                Next
                Return w
            End Function

            Private Function NormalizeRow(values As String()) As String()
                Dim row(ColumnCount - 1) As String

                If values IsNot Nothing Then
                    Dim n As Integer = Math.Min(values.Length, ColumnCount)
                    For i As Integer = 0 To n - 1
                        row(i) = If(values(i), "")
                    Next
                End If

                For i As Integer = 0 To ColumnCount - 1
                    If row(i) Is Nothing Then row(i) = ""
                Next

                Return row
            End Function

            ' ==========================================================
            ' PRINTING (self-contained)
            ' ==========================================================

            Private Sub PrintTitles()
                If Titles Is Nothing OrElse Titles.Count = 0 Then Return
                If ColumnCount <= 0 Then Return

                ' Largura total de uma linha da tabela (igual ao que as linhas "│...│" ocupam)
                Dim widths() As Integer = ColumnWidths()

                Dim totalLen As Integer = 1 ' "│" inicial
                For Each w As Integer In widths
                    totalLen += (w + 3) ' " " + content(w) + " " + "│"
                Next
                ' totalLen agora == comprimento de uma linha normal da tabela

                ' Área útil de texto dentro de "│ "  ...  " │"
                Dim textWidth As Integer = totalLen - 4
                If textWidth < 0 Then textWidth = 0

                For Each title In Titles

                    Dim raw As String = If(title.text, "")

                    ' Quebras explícitas do usuário viram "parágrafos"
                    Dim paragraphs As String() = raw.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf).Split({vbLf}, StringSplitOptions.None)

                    For Each para As String In paragraphs

                        Dim words As String() = para.Split(New Char() {" "c, ControlChars.Tab},
                                               StringSplitOptions.RemoveEmptyEntries)

                        Dim line As String = ""

                        For Each word As String In words

                            If line.Length = 0 Then
                                ' Linha vazia: tenta colocar a palavra
                                If word.Length <= textWidth Then
                                    line = word
                                Else
                                    ' Palavra não cabe nem numa linha vazia -> imprime mesmo assim
                                    PrintTitleLine(word, title.color, textWidth)
                                    line = ""
                                End If
                            Else
                                Dim candidate As String = line & " " & word

                                If candidate.Length <= textWidth Then
                                    line = candidate
                                Else
                                    ' Fecha a linha atual
                                    PrintTitleLine(line, title.color, textWidth)

                                    ' Começa nova linha com a palavra
                                    If word.Length <= textWidth Then
                                        line = word
                                    Else
                                        ' Palavra não cabe nem na linha vazia -> imprime mesmo assim
                                        PrintTitleLine(word, title.color, textWidth)
                                        line = ""
                                    End If
                                End If
                            End If

                        Next

                        ' Flush do resto
                        If line.Length > 0 Then
                            PrintTitleLine(line, title.color, textWidth)
                        End If

                        ' Se o parágrafo era vazio (ex.: linha em branco), ainda imprime uma linha vazia
                        If words.Length = 0 Then
                            PrintTitleLine("", title.color, textWidth)
                        End If

                    Next
                Next
            End Sub

            Private Shared Sub PrintTitleLine(text As String, color As ConsoleColor, textWidth As Integer)
                Console.ForegroundColor = ConsoleColor.DarkGray
                Console.Write("│ ")
                Console.ResetColor()

                Console.ForegroundColor = color
                Console.Write(text)
                Console.ResetColor()

                ' Completa com espaços até fechar a largura útil (se o texto for menor)
                If text.Length < textWidth Then
                    Console.Write(New String(" "c, textWidth - text.Length))
                End If

                Console.ForegroundColor = ConsoleColor.DarkGray
                Console.WriteLine(" │")
                Console.ResetColor()
            End Sub

            Public Sub Print()
                If ColumnCount <= 0 Then Return

                Dim widths() As Integer = ColumnWidths()

                ' Linha de cima dos títulos: totalmente horizontal, sem pontas
                Dim upper As String = BuildBorder("┌"c, "─"c, "┐"c, widths)

                ' Linha que separa títulos do header (a “top border” do grid de colunas)
                Dim top As String = BuildBorder("├"c, "┬"c, "┤"c, widths)

                Dim mid As String = BuildBorder("├"c, "┼"c, "┤"c, widths)
                Dim bot As String = BuildBorder("└"c, "┴"c, "┘"c, widths)

                Console.ForegroundColor = ConsoleColor.DarkGray
                Console.WriteLine(upper)
                Console.ResetColor()

                ' Titles (entre upper e top)
                PrintTitles()

                Console.ForegroundColor = ConsoleColor.DarkGray
                Console.WriteLine(top)
                Console.ResetColor()

                ' Header
                PrintRowInternal(
        values:=Me.Columns.ToArray(),
        widths:=widths,
        aligns:=Me.ColumnsAlignment,
        perColumnColors:=Me.ColumnHeaderColors,
        defaultColor:=ConsoleColor.Cyan
    )

                ' Separator before body/footer
                If Me.RowCount > 0 OrElse Me.HasFooter Then
                    Console.ForegroundColor = ConsoleColor.DarkGray
                    Console.WriteLine(mid)
                    Console.ResetColor()
                End If

                ' Body
                For Each row As String() In Me.Cells
                    PrintRowInternal(
            values:=row,
            widths:=widths,
            aligns:=Me.ColumnsAlignment,
            perColumnColors:=Me.ColumnCellColor,
            defaultColor:=ConsoleColor.Gray
        )
                Next

                ' Footer
                If Me.HasFooter Then
                    If Me.RowCount > 0 Then
                        Console.ForegroundColor = ConsoleColor.DarkGray
                        Console.WriteLine(mid)
                        Console.ResetColor()
                    End If

                    PrintFooterRowInternal(
            values:=Me.FooterCells,
            widths:=widths,
            aligns:=Me.ColumnsAlignment,
            color:=Me.FooterColor
        )
                End If

                Console.ForegroundColor = ConsoleColor.DarkGray
                Console.WriteLine(bot)
                Console.ResetColor()
            End Sub

            Private Shared Function BuildBorder(left As Char, mid As Char, right As Char, widths() As Integer) As String
                Dim sb As New System.Text.StringBuilder()
                sb.Append(left)
                For i As Integer = 0 To widths.Length - 1
                    sb.Append(New String("─"c, widths(i) + 2)) ' padding
                    sb.Append(If(i = widths.Length - 1, right, mid))
                Next
                Return sb.ToString()
            End Function

            Private Shared Sub PrintRowInternal(values As String(),
                                        widths() As Integer,
                                        aligns As List(Of Enum_Alignment),
                                        perColumnColors As List(Of ConsoleColor),
                                        defaultColor As ConsoleColor)

                Console.ForegroundColor = ConsoleColor.DarkGray
                Console.Write("│")
                Console.ResetColor()

                For i As Integer = 0 To widths.Length - 1

                    Dim txt As String = ""
                    If values IsNot Nothing AndAlso i < values.Length AndAlso values(i) IsNot Nothing Then
                        txt = values(i)
                    End If

                    Dim align As Enum_Alignment = Enum_Alignment.Left
                    If aligns IsNot Nothing AndAlso i < aligns.Count Then align = aligns(i)

                    Dim col As ConsoleColor = defaultColor
                    If perColumnColors IsNot Nothing AndAlso i < perColumnColors.Count Then col = perColumnColors(i)

                    Dim content As String = AlignText(txt, widths(i), align)

                    Console.Write(" ")
                    Console.ForegroundColor = col
                    Console.Write(content)
                    Console.ResetColor()
                    Console.Write(" ")

                    Console.ForegroundColor = ConsoleColor.DarkGray
                    Console.Write("│")
                    Console.ResetColor()
                Next

                Console.WriteLine()
            End Sub

            Private Shared Sub PrintFooterRowInternal(values As String(),
                                              widths() As Integer,
                                              aligns As List(Of Enum_Alignment),
                                              color As ConsoleColor)

                Console.ForegroundColor = ConsoleColor.DarkGray
                Console.Write("│")
                Console.ResetColor()

                For i As Integer = 0 To widths.Length - 1

                    Dim txt As String = ""
                    If values IsNot Nothing AndAlso i < values.Length AndAlso values(i) IsNot Nothing Then
                        txt = values(i)
                    End If

                    Dim align As Enum_Alignment = Enum_Alignment.Left
                    If aligns IsNot Nothing AndAlso i < aligns.Count Then align = aligns(i)

                    Dim content As String = AlignText(txt, widths(i), align)

                    Console.Write(" ")
                    Console.ForegroundColor = color
                    Console.Write(content)
                    Console.ResetColor()
                    Console.Write(" ")

                    Console.ForegroundColor = ConsoleColor.DarkGray
                    Console.Write("│")
                    Console.ResetColor()
                Next

                Console.WriteLine()
            End Sub

            Private Shared Function AlignText(txt As String, width As Integer, align As Enum_Alignment) As String
                Dim t As String = If(txt, "")
                If width < 0 Then width = 0

                If t.Length > width Then
                    t = t.Substring(0, width)
                End If

                Select Case align
                    Case Enum_Alignment.Right
                        Return t.PadLeft(width)

                    Case Enum_Alignment.Center
                        Dim pad As Integer = width - t.Length
                        If pad <= 0 Then Return t
                        Dim leftPad As Integer = pad \ 2
                        Dim rightPad As Integer = pad - leftPad
                        Return New String(" "c, leftPad) & t & New String(" "c, rightPad)

                    Case Else
                        Return t.PadRight(width)
                End Select
            End Function

        End Class

#End Region

    End Class

End Namespace