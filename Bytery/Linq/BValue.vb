Imports System.Globalization

Namespace Linq

    ''' <summary>
    ''' Base class for all scalar LINQ tokens.
    '''
    ''' <para>
    ''' <see cref="BValue"/> represents non-container values such as numbers, booleans,
    ''' dates, strings, bytes, and null-like scalar wrappers.
    ''' </para>
    '''
    ''' <para>
    ''' This type centralizes scalar conversion rules so each concrete token class can keep
    ''' a small implementation focused on storage and formatting.
    ''' </para>
    ''' </summary>
    Public MustInherit Class BValue
        Inherits BToken

        ''' <summary>
        ''' Gets whether this token is a primitive scalar.
        ''' For all <see cref="BValue"/> descendants, this is always <c>True</c>.
        ''' </summary>
        Public Overrides ReadOnly Property IsPrimitive As Boolean
            Get
                Return True
            End Get
        End Property

        ''' <summary>
        ''' Gets whether this token represents an array.
        ''' Primitive value tokens are never arrays.
        ''' </summary>
        Public Overrides ReadOnly Property IsArray As Boolean
            Get
                Return False
            End Get
        End Property

#Region "Primitive conversion helpers"

        ''' <summary>
        ''' Converts a nullable numeric payload to the requested CLR target type.
        '''
        ''' Supported targets:
        ''' <list type="bullet">
        ''' <item><description>Numeric CLR types</description></item>
        ''' <item><description><c>Nullable(Of T)</c> for numeric CLR types</description></item>
        ''' <item><description><see cref="Object"/></description></item>
        ''' </list>
        '''
        ''' Null conversion is allowed only for reference targets and nullable value types.
        ''' </summary>
        Protected Shared Function ConvertNumberValue(Of T)(value As Double?, tokenName As String) As T

            Dim targetType As Type = GetType(T)
            Dim nullableType As Type = Nullable.GetUnderlyingType(targetType)
            Dim effectiveType As Type = If(nullableType, targetType)
            Dim isNullable As Boolean = (nullableType IsNot Nothing)

            If Not value.HasValue Then
                If isNullable OrElse Not effectiveType.IsValueType Then
                    Return CType(Nothing, T)
                End If

                Throw New InvalidCastException($"Cannot convert null {tokenName} to {targetType.FullName}.")
            End If

            Dim raw As Double = value.Value
            Dim boxed As Object

            If effectiveType Is GetType(Object) Then
                boxed = raw
            ElseIf effectiveType Is GetType(Double) Then
                boxed = raw
            ElseIf effectiveType Is GetType(Single) Then
                boxed = Convert.ToSingle(raw)
            ElseIf effectiveType Is GetType(Decimal) Then
                boxed = Convert.ToDecimal(raw, CultureInfo.InvariantCulture)
            ElseIf effectiveType Is GetType(Byte) Then
                boxed = Convert.ToByte(raw)
            ElseIf effectiveType Is GetType(SByte) Then
                boxed = Convert.ToSByte(raw)
            ElseIf effectiveType Is GetType(Short) Then
                boxed = Convert.ToInt16(raw)
            ElseIf effectiveType Is GetType(UShort) Then
                boxed = Convert.ToUInt16(raw)
            ElseIf effectiveType Is GetType(Integer) Then
                boxed = Convert.ToInt32(raw)
            ElseIf effectiveType Is GetType(UInteger) Then
                boxed = Convert.ToUInt32(raw)
            ElseIf effectiveType Is GetType(Long) Then
                boxed = Convert.ToInt64(raw)
            ElseIf effectiveType Is GetType(ULong) Then
                boxed = Convert.ToUInt64(raw)
            Else
                Throw New InvalidCastException(
                    $"Cannot convert {tokenName} to {targetType.FullName}. " &
                    $"Supported targets are numeric CLR types, Nullable(Of numeric), and Object.")
            End If

            If isNullable Then
                Dim wrapped As Object = Activator.CreateInstance(targetType, boxed)
                Return CType(wrapped, T)
            End If

            Return CType(boxed, T)

        End Function

        ''' <summary>
        ''' Converts a nullable boolean payload to the requested CLR target type.
        '''
        ''' Supported targets:
        ''' <list type="bullet">
        ''' <item><description><see cref="Boolean"/></description></item>
        ''' <item><description><c>Nullable(Of Boolean)</c></description></item>
        ''' <item><description><see cref="Object"/></description></item>
        ''' </list>
        ''' </summary>
        Protected Shared Function ConvertBooleanValue(Of T)(value As Boolean?, tokenName As String) As T

            Dim targetType As Type = GetType(T)
            Dim nullableType As Type = Nullable.GetUnderlyingType(targetType)
            Dim effectiveType As Type = If(nullableType, targetType)
            Dim isNullable As Boolean = (nullableType IsNot Nothing)

            If Not value.HasValue Then
                If isNullable OrElse Not effectiveType.IsValueType Then
                    Return CType(Nothing, T)
                End If

                Throw New InvalidCastException($"Cannot convert null {tokenName} to {targetType.FullName}.")
            End If

            Dim boxed As Object

            If effectiveType Is GetType(Object) Then
                boxed = value.Value
            ElseIf effectiveType Is GetType(Boolean) Then
                boxed = value.Value
            Else
                Throw New InvalidCastException(
                    $"Cannot convert {tokenName} to {targetType.FullName}. " &
                    $"Supported targets are Boolean, Nullable(Of Boolean), and Object.")
            End If

            If isNullable Then
                Dim wrapped As Object = Activator.CreateInstance(targetType, boxed)
                Return CType(wrapped, T)
            End If

            Return CType(boxed, T)

        End Function

        ''' <summary>
        ''' Converts a nullable date payload to the requested CLR target type.
        '''
        ''' Supported targets:
        ''' <list type="bullet">
        ''' <item><description><see cref="Date"/></description></item>
        ''' <item><description><c>Nullable(Of Date)</c></description></item>
        ''' <item><description><see cref="DateTimeOffset"/></description></item>
        ''' <item><description><c>Nullable(Of DateTimeOffset)</c></description></item>
        ''' <item><description><see cref="Object"/></description></item>
        ''' </list>
        ''' </summary>
        Protected Shared Function ConvertDateValue(Of T)(value As Date?, tokenName As String) As T

            Dim targetType As Type = GetType(T)
            Dim nullableType As Type = Nullable.GetUnderlyingType(targetType)
            Dim effectiveType As Type = If(nullableType, targetType)
            Dim isNullable As Boolean = (nullableType IsNot Nothing)

            If Not value.HasValue Then
                If isNullable OrElse Not effectiveType.IsValueType Then
                    Return CType(Nothing, T)
                End If

                Throw New InvalidCastException($"Cannot convert null {tokenName} to {targetType.FullName}.")
            End If

            Dim boxed As Object

            If effectiveType Is GetType(Object) Then
                boxed = value.Value
            ElseIf effectiveType Is GetType(Date) Then
                boxed = value.Value
            ElseIf effectiveType Is GetType(DateTimeOffset) Then
                boxed = New DateTimeOffset(value.Value)
            Else
                Throw New InvalidCastException(
                    $"Cannot convert {tokenName} to {targetType.FullName}. " &
                    $"Supported targets are Date, Nullable(Of Date), DateTimeOffset, Nullable(Of DateTimeOffset), and Object.")
            End If

            If isNullable Then
                Dim wrapped As Object = Activator.CreateInstance(targetType, boxed)
                Return CType(wrapped, T)
            End If

            Return CType(boxed, T)

        End Function

        ''' <summary>
        ''' Converts a string payload to the requested CLR target type.
        '''
        ''' Supported targets:
        ''' <list type="bullet">
        ''' <item><description><see cref="String"/></description></item>
        ''' <item><description><see cref="Char"/></description></item>
        ''' <item><description><c>Nullable(Of Char)</c></description></item>
        ''' <item><description><see cref="Object"/></description></item>
        ''' </list>
        '''
        ''' <para>
        ''' Char conversion requires the source string to contain exactly one character.
        ''' </para>
        ''' </summary>
        Protected Shared Function ConvertStringValue(Of T)(value As String, tokenName As String) As T

            Dim targetType As Type = GetType(T)
            Dim nullableType As Type = Nullable.GetUnderlyingType(targetType)
            Dim effectiveType As Type = If(nullableType, targetType)
            Dim isNullable As Boolean = (nullableType IsNot Nothing)

            If value Is Nothing Then
                If isNullable OrElse Not effectiveType.IsValueType Then
                    Return CType(Nothing, T)
                End If

                Throw New InvalidCastException($"Cannot convert null {tokenName} to {targetType.FullName}.")
            End If

            Dim boxed As Object

            If effectiveType Is GetType(Object) Then
                boxed = value
            ElseIf effectiveType Is GetType(String) Then
                boxed = value
            ElseIf effectiveType Is GetType(Char) Then
                If value.Length <> 1 Then
                    Throw New InvalidCastException($"Cannot convert {tokenName} value ""{value}"" to Char because its length is not 1.")
                End If
                boxed = value(0)
            Else
                Throw New InvalidCastException(
                    $"Cannot convert {tokenName} to {targetType.FullName}. " &
                    $"Supported targets are String, Char, Nullable(Of Char), and Object.")
            End If

            If isNullable Then
                Dim wrapped As Object = Activator.CreateInstance(targetType, boxed)
                Return CType(wrapped, T)
            End If

            Return CType(boxed, T)

        End Function

        ''' <summary>
        ''' Converts a byte-array payload to the requested CLR target type.
        '''
        ''' Supported targets:
        ''' <list type="bullet">
        ''' <item><description><c>Byte()</c></description></item>
        ''' <item><description><see cref="Object"/></description></item>
        ''' </list>
        ''' </summary>
        Protected Shared Function ConvertBytesValue(Of T)(value As Byte(), tokenName As String) As T

            Dim targetType As Type = GetType(T)

            If value Is Nothing Then
                If Not targetType.IsValueType OrElse Nullable.GetUnderlyingType(targetType) IsNot Nothing Then
                    Return CType(Nothing, T)
                End If

                Throw New InvalidCastException($"Cannot convert null {tokenName} to {targetType.FullName}.")
            End If

            If targetType Is GetType(Object) OrElse targetType Is GetType(Byte()) Then
                Return CType(CType(value, Object), T)
            End If

            Throw New InvalidCastException(
                $"Cannot convert {tokenName} to {targetType.FullName}. " &
                $"Supported targets are Byte() and Object.")

        End Function

#End Region

        ''' <summary>
        ''' Resolves a path continuation over a primitive token.
        '''
        ''' Primitive values cannot be traversed any further. When the caller marked the
        ''' path as optional, the supplied default value is returned instead of throwing.
        ''' </summary>
        Friend Overrides Function GetValue(Of T)(paths As String(), pathIndex As Integer, pathOptional As Boolean, [default] As T) As T

            If paths Is Nothing OrElse paths.Length = 0 Then
                Throw New ArgumentException("paths cannot be null or empty.", NameOf(paths))
            End If

            If pathIndex < 0 OrElse pathIndex >= paths.Length Then
                Throw New ArgumentOutOfRangeException(NameOf(pathIndex))
            End If

            If pathOptional Then
                Return [default]
            End If

            Throw New Exception("Cannot continue path after primitive value: " & String.Join(".", paths))

        End Function

        ''' <summary>
        ''' Returns the default textual representation for a primitive value token.
        ''' Indentation is ignored because primitives render as a single JSON value.
        ''' </summary>
        Public Overrides Function ToString(indent As Boolean) As String
            Return Me.ToString
        End Function

    End Class

End Namespace