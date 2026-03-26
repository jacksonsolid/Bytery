Imports System.IO
Imports Bytery.JSON

Namespace Encoding

    ''' <summary>
    ''' Session-local schema entry written into the session schema table.
    ''' </summary>
    ''' <remarks>
    ''' This type wraps a global <see cref="JsonSchema"/> and augments it with
    ''' session-dependent data that only exists during encoding.
    '''
    ''' Session-only state includes:
    ''' - <see cref="Index"/>: position of this schema inside the current session schema table.
    ''' - <see cref="Pointer"/>: encoded schema pointer for this session entry.
    ''' - <see cref="Fields"/>: object-field entries resolved for this session.
    ''' - <see cref="DictValueSchemaPtr"/> / <see cref="ArrayValueSchemaPtr"/>:
    '''   resolved schema pointers for object-like map/array value types.
    '''
    ''' The underlying logical schema remains stored in <see cref="JS"/>.
    ''' </remarks>
    Friend NotInheritable Class SessionSchema

        ''' <summary>
        ''' Global logical schema definition associated with this session entry.
        ''' </summary>
        Public ReadOnly JS As JsonSchema

        ''' <summary>
        ''' Zero-based position of this schema in the current session schema table.
        ''' </summary>
        Public Index As Integer

        ''' <summary>
        ''' Encoded pointer that references this schema inside the current session.
        ''' </summary>
        Public Pointer As PTR

        ''' <summary>
        ''' Session field entries for object schemas.
        ''' </summary>
        ''' <remarks>
        ''' Only meaningful when <see cref="JS.Kind"/> is <see cref="JsonSchema.SchemaKind.Object"/>.
        ''' For map and array schemas this is typically empty.
        ''' </remarks>
        Public Fields As SessionField()

        ''' <summary>
        ''' Resolved session schema pointer for map values whose base type is Object.
        ''' </summary>
        ''' <remarks>
        ''' Used only when <see cref="JS.Kind"/> is <see cref="JsonSchema.SchemaKind.Map"/>
        ''' and the declared map value base type is Object.
        ''' </remarks>
        Public DictValueSchemaPtr As PTR

        ''' <summary>
        ''' Resolved session schema pointer for array elements whose type is Object.
        ''' </summary>
        ''' <remarks>
        ''' Used only when <see cref="JS.Kind"/> is <see cref="JsonSchema.SchemaKind.Array"/>
        ''' and the element type is Object.
        ''' </remarks>
        Public ArrayValueSchemaPtr As PTR

        ''' <summary>
        ''' Creates a session schema wrapper for the supplied global schema.
        ''' </summary>
        ''' <param name="js">Global logical schema definition.</param>
        Public Sub New(js As JsonSchema)
            Me.JS = js
            Me.Fields = Array.Empty(Of SessionField)()
        End Sub

#Region "WriteToStream"

        ''' <summary>
        ''' Writes this schema entry into the session schema table.
        ''' </summary>
        ''' <param name="ms">Destination stream.</param>
        ''' <param name="s">Current encoding session.</param>
        ''' <remarks>
        ''' The output layout depends on <see cref="JS.Kind"/>:
        '''
        ''' - Map:
        '''   [SMAT map-or-map-array tag]
        '''   [schema pointer only when map value base type is Object]
        '''
        ''' - Array:
        '''   [SMAT array tag]
        '''   [schema pointer only when element type is Object]
        '''
        ''' - Object:
        '''   [SMAT object header]
        '''   [N field entries]
        '''
        ''' All tags and pointer encodings must match the constants defined by the wire format.
        ''' </remarks>
        Public Sub WriteToStream(ms As Stream, s As ISession)

            Select Case JS.Kind

                Case JsonSchema.SchemaKind.Map

                    Dim vt As JsonFieldType = JS.DictValueType
                    Dim isArr As Boolean = JsonField.FieldTypeIsArray(vt)
                    Dim baseVal As JsonFieldType = JsonField.FieldTypeWithoutArrayFlag(vt)

                    Dim ord As Integer = baseVal - 1
                    If ord < 0 Then
                        Throw New Exception("Invalid map valueType for SMAT: " & vt.ToString())
                    End If

                    Dim smat As Byte = If(isArr, CByte(SMAT_MAP_ARR_INTEGER + ord), CByte(SMAT_MAP_INTEGER + ord))

                    ms.WriteByte(smat)

                    If baseVal = JsonFieldType.Object Then
                        ms.Write(DictValueSchemaPtr.len)
                        ms.Write(DictValueSchemaPtr.data)
                    End If

                Case JsonSchema.SchemaKind.Array

                    Dim et As JsonFieldType = JS.ArrayValueType

                    If JsonField.FieldTypeIsArray(et) Then
                        Throw New Exception("Array schema elemType cannot have ArrayFlag: " & et.ToString())
                    End If

                    Dim ord As Integer = et - 1
                    If ord < 0 Then
                        Throw New Exception("Invalid array elemType for SMAT: " & et.ToString())
                    End If

                    Dim smat As Byte = CByte(SMAT_ARR_INTEGER + ord)
                    ms.WriteByte(smat)

                    If et = JsonFieldType.Object Then
                        ms.Write(ArrayValueSchemaPtr.len)
                        ms.Write(ArrayValueSchemaPtr.data)
                    End If

                Case Else

                    Dim count As Integer = If(Me.Fields Is Nothing, 0, Me.Fields.Length)
                    Writer.WriteSmatObject(ms, count)

                    For i As Integer = 0 To count - 1
                        Me.Fields(i).WriteToStream(ms, s)
                    Next

            End Select

        End Sub

#End Region

    End Class

End Namespace