Imports System.Globalization
Imports System.Text

Namespace Linq

    ''' <summary>
    ''' Strict JSON parser that materializes a <see cref="BToken"/> tree.
    ''' </summary>
    ''' <remarks>
    ''' Supported JSON constructs:
    ''' <list type="bullet">
    ''' <item><description>Objects</description></item>
    ''' <item><description>Arrays</description></item>
    ''' <item><description>Strings with standard JSON escape sequences</description></item>
    ''' <item><description>Numbers parsed as <see cref="Double"/></description></item>
    ''' <item><description><c>true</c>, <c>false</c>, and <c>null</c></description></item>
    ''' </list>
    '''
    ''' This parser is intentionally strict:
    ''' <list type="bullet">
    ''' <item><description>No comments</description></item>
    ''' <item><description>No trailing commas</description></item>
    ''' <item><description>No automatic Date parsing from strings</description></item>
    ''' <item><description>Duplicate object keys are allowed; the last value wins</description></item>
    ''' </list>
    ''' </remarks>
    Friend NotInheritable Class BTokenParser

        ''' <summary>
        ''' Original JSON text being parsed.
        ''' </summary>
        Private ReadOnly _json As String

        ''' <summary>
        ''' Cached length of <see cref="_json"/> to avoid repeated property access.
        ''' </summary>
        Private ReadOnly _length As Integer

        ''' <summary>
        ''' Current read cursor inside <see cref="_json"/>.
        ''' </summary>
        Private _index As Integer

        ''' <summary>
        ''' Initializes a new parser instance for the supplied JSON text.
        ''' </summary>
        Private Sub New(json As String)
            If json Is Nothing Then Throw New ArgumentNullException(NameOf(json))
            _json = json
            _length = json.Length
            _index = 0
        End Sub

#Region "Public API"

        ''' <summary>
        ''' Parses any valid JSON value and returns the corresponding <see cref="BToken"/>.
        ''' </summary>
        Public Shared Function Parse(json As String) As BToken

            Dim p As New BTokenParser(json)
            Dim token As BToken = p.ParseValue()
            p.SkipWhite()

            If Not p.IsEOF() Then
                Throw p.BuildError("Unexpected trailing characters after valid JSON.")
            End If

            Return token

        End Function

        ''' <summary>
        ''' Parses JSON and requires the root value to be an object.
        ''' </summary>
        Public Shared Function ParseObject(json As String) As BObject

            Dim token As BToken = Parse(json)
            Dim obj As BObject = TryCast(token, BObject)

            If obj Is Nothing Then
                Throw New Exception("JSON root is not an object.")
            End If

            Return obj

        End Function

        ''' <summary>
        ''' Parses JSON and requires the root value to be an array.
        ''' </summary>
        Public Shared Function ParseArray(json As String) As BArray

            Dim token As BToken = Parse(json)
            Dim arr As BArray = TryCast(token, BArray)

            If arr Is Nothing Then
                Throw New Exception("JSON root is not an array.")
            End If

            Return arr

        End Function

#End Region

#Region "Core Value Parsing"

        ''' <summary>
        ''' Parses the next JSON value at the current cursor position.
        ''' </summary>
        Private Function ParseValue() As BToken

            SkipWhite()

            If IsEOF() Then
                Throw BuildError("Unexpected end of JSON while reading a value.")
            End If

            Select Case _json(_index)

                Case "{"c
                    Return ParseObjectCore()

                Case "["c
                    Return ParseArrayCore()

                Case """"c
                    Return New BString(ParseStringLiteral())

                Case "t"c
                    ReadExpected("true")
                    Return New BBoolean(True)

                Case "f"c
                    ReadExpected("false")
                    Return New BBoolean(False)

                Case "n"c
                    ReadExpected("null")
                    Return BNull.Instance(JSON.JsonFieldType.Unknown)

                Case "-"c, "0"c, "1"c, "2"c, "3"c, "4"c, "5"c, "6"c, "7"c, "8"c, "9"c
                    Return ParseNumberToken()

                Case Else
                    Throw BuildError("Invalid JSON value.")
            End Select

        End Function

        ''' <summary>
        ''' Parses a JSON object starting at the current cursor.
        ''' </summary>
        Private Function ParseObjectCore() As BObject

            Dim obj As New BObject()

            Expect("{"c)
            SkipWhite()

            If TryConsume("}"c) Then
                Return obj
            End If

            Do

                SkipWhite()

                If IsEOF() OrElse _json(_index) <> """"c Then
                    Throw BuildError("Expected a string property name.")
                End If

                Dim name As String = ParseStringLiteral()

                SkipWhite()
                Expect(":"c)

                Dim value As BToken = ParseValue()

                obj(name) = value

                SkipWhite()

                If TryConsume("}"c) Then
                    Exit Do
                End If

                Expect(","c)

            Loop

            Return obj

        End Function

        ''' <summary>
        ''' Parses a JSON array starting at the current cursor.
        ''' </summary>
        Private Function ParseArrayCore() As BArray

            Dim arr As New BArray(JSON.JsonFieldType.Unknown)

            Expect("["c)
            SkipWhite()

            If TryConsume("]"c) Then
                Return arr
            End If

            Do

                Dim value As BToken = ParseValue()
                arr.Add(value)

                SkipWhite()

                If TryConsume("]"c) Then
                    Exit Do
                End If

                Expect(","c)

            Loop

            Return arr

        End Function

#End Region

#Region "Number Parsing"

        ''' <summary>
        ''' Parses a JSON number and stores it as <see cref="BNumber"/>.
        ''' </summary>
        ''' <remarks>
        ''' JSON numeric grammar handled here:
        ''' <code>
        ''' -? (0 | [1-9][0-9]*) ( "." [0-9]+ )? ( [eE] [+-]? [0-9]+ )?
        ''' </code>
        ''' </remarks>
        Private Function ParseNumberToken() As BToken

            Dim start As Integer = _index

            If _json(_index) = "-"c Then
                _index += 1
                If IsEOF() Then
                    Throw BuildError("Invalid number.")
                End If
            End If

            If _json(_index) = "0"c Then

                _index += 1

                If Not IsEOF() AndAlso IsDigit(_json(_index)) Then
                    Throw BuildError("Invalid number: leading zeros are not allowed.")
                End If

            ElseIf IsDigit1To9(_json(_index)) Then

                _index += 1

                While Not IsEOF() AndAlso IsDigit(_json(_index))
                    _index += 1
                End While

            Else
                Throw BuildError("Invalid number.")
            End If

            If Not IsEOF() AndAlso _json(_index) = "."c Then

                _index += 1

                If IsEOF() OrElse Not IsDigit(_json(_index)) Then
                    Throw BuildError("Invalid number: expected digits after decimal point.")
                End If

                While Not IsEOF() AndAlso IsDigit(_json(_index))
                    _index += 1
                End While

            End If

            If Not IsEOF() AndAlso (_json(_index) = "e"c OrElse _json(_index) = "E"c) Then

                _index += 1

                If Not IsEOF() AndAlso (_json(_index) = "+"c OrElse _json(_index) = "-"c) Then
                    _index += 1
                End If

                If IsEOF() OrElse Not IsDigit(_json(_index)) Then
                    Throw BuildError("Invalid number: expected exponent digits.")
                End If

                While Not IsEOF() AndAlso IsDigit(_json(_index))
                    _index += 1
                End While

            End If

            Dim value As Double
            Dim text As String = _json.Substring(start, _index - start)

            If Not Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, value) Then
                Throw BuildError("Invalid numeric value.")
            End If

            If Double.IsNaN(value) OrElse Double.IsInfinity(value) Then
                Throw BuildError("Invalid numeric value.")
            End If

            Return New BNumber(value)

        End Function

#End Region

#Region "String Parsing"

        ''' <summary>
        ''' Parses a JSON string literal, including escape sequences and unicode escapes.
        ''' </summary>
        ''' <remarks>
        ''' This method supports:
        ''' <list type="bullet">
        ''' <item><description>Simple escapes such as <c>\n</c>, <c>\t</c>, <c>\\</c>, and <c>\"</c></description></item>
        ''' <item><description><c>\uXXXX</c> unicode escapes</description></item>
        ''' <item><description>Surrogate-pair decoding for non-BMP characters</description></item>
        ''' </list>
        ''' </remarks>
        Private Function ParseStringLiteral() As String

            Expect(""""c)

            Dim segmentStart As Integer = _index
            Dim sb As StringBuilder = Nothing

            While Not IsEOF()

                Dim ch As Char = _json(_index)

                If ch = """"c Then

                    If sb Is Nothing Then
                        Dim s As String = _json.Substring(segmentStart, _index - segmentStart)
                        _index += 1
                        Return s
                    End If

                    If _index > segmentStart Then
                        sb.Append(_json, segmentStart, _index - segmentStart)
                    End If

                    _index += 1
                    Return sb.ToString()

                End If

                If ch = "\"c Then

                    If sb Is Nothing Then
                        sb = New StringBuilder(Math.Max(16, (_index - segmentStart) + 8))
                    End If

                    If _index > segmentStart Then
                        sb.Append(_json, segmentStart, _index - segmentStart)
                    End If

                    _index += 1

                    If IsEOF() Then
                        Throw BuildError("Unterminated escape sequence in string.")
                    End If

                    Dim esc As Char = _json(_index)

                    Select Case esc
                        Case """"c : sb.Append(""""c) : _index += 1
                        Case "\"c : sb.Append("\"c) : _index += 1
                        Case "/"c : sb.Append("/"c) : _index += 1
                        Case "b"c : sb.Append(ControlChars.Back) : _index += 1
                        Case "f"c : sb.Append(ControlChars.FormFeed) : _index += 1
                        Case "n"c : sb.Append(ControlChars.Lf) : _index += 1
                        Case "r"c : sb.Append(ControlChars.Cr) : _index += 1
                        Case "t"c : sb.Append(ControlChars.Tab) : _index += 1

                        Case "u"c

                            _index += 1
                            Dim high As Integer = ReadHexQuad()

                            If high >= &HD800 AndAlso high <= &HDBFF Then

                                If _index + 1 >= _length OrElse _json(_index) <> "\"c OrElse _json(_index + 1) <> "u"c Then
                                    Throw BuildError("High surrogate must be followed by a low surrogate.")
                                End If

                                _index += 2
                                Dim low As Integer = ReadHexQuad()

                                If low < &HDC00 OrElse low > &HDFFF Then
                                    Throw BuildError("Invalid low surrogate in unicode escape.")
                                End If

                                Dim codePoint As Integer =
                                    &H10000 + ((high - &HD800) << 10) + (low - &HDC00)

                                sb.Append(Char.ConvertFromUtf32(codePoint))

                            ElseIf high >= &HDC00 AndAlso high <= &HDFFF Then
                                Throw BuildError("Unexpected low surrogate without preceding high surrogate.")
                            Else
                                sb.Append(ChrW(high))
                            End If

                        Case Else
                            Throw BuildError("Invalid escape sequence in string.")
                    End Select

                    segmentStart = _index
                    Continue While

                End If

                If AscW(ch) < 32 Then
                    Throw BuildError("Unescaped control character in string.")
                End If

                _index += 1

            End While

            Throw BuildError("Unterminated string literal.")

        End Function

#End Region

#Region "Cursor Helpers"

        ''' <summary>
        ''' Advances past JSON whitespace characters.
        ''' </summary>
        Private Sub SkipWhite()

            While _index < _length
                Select Case _json(_index)
                    Case " "c, ControlChars.Tab, ControlChars.Cr, ControlChars.Lf
                        _index += 1
                    Case Else
                        Exit While
                End Select
            End While

        End Sub

        ''' <summary>
        ''' Consumes the given character if it is present at the current cursor.
        ''' </summary>
        Private Function TryConsume(ch As Char) As Boolean

            If _index < _length AndAlso _json(_index) = ch Then
                _index += 1
                Return True
            End If

            Return False

        End Function

        ''' <summary>
        ''' Requires the given character at the current cursor and consumes it.
        ''' </summary>
        Private Sub Expect(ch As Char)

            If IsEOF() OrElse _json(_index) <> ch Then
                Throw BuildError("Expected '" & ch & "'.")
            End If

            _index += 1

        End Sub

        ''' <summary>
        ''' Requires an exact token such as <c>true</c>, <c>false</c>, or <c>null</c>.
        ''' </summary>
        Private Sub ReadExpected(text As String)

            If String.IsNullOrEmpty(text) Then
                Throw New ArgumentNullException(NameOf(text))
            End If

            If _index + text.Length > _length Then
                Throw BuildError("Unexpected end of JSON.")
            End If

            For i As Integer = 0 To text.Length - 1
                If _json(_index + i) <> text(i) Then
                    Throw BuildError("Invalid token.")
                End If
            Next

            _index += text.Length

        End Sub

        ''' <summary>
        ''' Reads exactly four hexadecimal digits from a <c>\uXXXX</c> escape sequence.
        ''' </summary>
        Private Function ReadHexQuad() As Integer

            If _index + 4 > _length Then
                Throw BuildError("Incomplete unicode escape sequence.")
            End If

            Dim value As Integer = 0

            For i As Integer = 0 To 3
                value = (value << 4) Or HexValue(_json(_index + i))
            Next

            _index += 4
            Return value

        End Function

        ''' <summary>
        ''' Converts a single hexadecimal character to its numeric value.
        ''' </summary>
        Private Shared Function HexValue(ch As Char) As Integer

            Select Case ch
                Case "0"c To "9"c
                    Return AscW(ch) - AscW("0"c)

                Case "a"c To "f"c
                    Return 10 + (AscW(ch) - AscW("a"c))

                Case "A"c To "F"c
                    Return 10 + (AscW(ch) - AscW("A"c))

                Case Else
                    Throw New Exception("Invalid hex digit.")
            End Select

        End Function

        ''' <summary>
        ''' Returns <c>True</c> when the character is in the range <c>0..9</c>.
        ''' </summary>
        Private Shared Function IsDigit(ch As Char) As Boolean
            Return ch >= "0"c AndAlso ch <= "9"c
        End Function

        ''' <summary>
        ''' Returns <c>True</c> when the character is in the range <c>1..9</c>.
        ''' </summary>
        Private Shared Function IsDigit1To9(ch As Char) As Boolean
            Return ch >= "1"c AndAlso ch <= "9"c
        End Function

        ''' <summary>
        ''' Returns <c>True</c> when the parser cursor reached the end of the input.
        ''' </summary>
        Private Function IsEOF() As Boolean
            Return _index >= _length
        End Function

#End Region

#Region "Error Reporting"

        ''' <summary>
        ''' Builds a parsing exception including line, column, and absolute index.
        ''' </summary>
        Private Function BuildError(message As String) As Exception

            Dim line As Integer = 1
            Dim column As Integer = 1

            For i As Integer = 0 To Math.Min(_index, _length) - 1
                Dim ch As Char = _json(i)

                If ch = ControlChars.Lf Then
                    line += 1
                    column = 1
                ElseIf ch <> ControlChars.Cr Then
                    column += 1
                End If
            Next

            Return New Exception(
                $"{message} Line {line}, column {column}, index {_index}.")

        End Function

#End Region

    End Class

End Namespace