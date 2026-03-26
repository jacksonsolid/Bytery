Imports System.IO
Imports Bytery.JSON

Namespace Encoding

    ''' <summary>
    ''' Session-local field descriptor used when writing object schemas to the session schema table.
    ''' </summary>
    ''' <remarks>
    ''' This type wraps a global <see cref="JsonField"/> and adds session-specific state that
    ''' cannot live in the global schema model.
    '''
    ''' In particular:
    ''' - <see cref="F"/> contains the logical field definition.
    ''' - <see cref="RefSchemaPtr"/> contains the resolved session pointer for object-like fields.
    '''
    ''' Primitive fields do not use <see cref="RefSchemaPtr"/>.
    ''' </remarks>
    Friend NotInheritable Class SessionField

        ''' <summary>
        ''' Global field definition associated with this session field.
        ''' </summary>
        Public ReadOnly F As JsonField

        ''' <summary>
        ''' Session-specific schema pointer for object or object-array fields.
        ''' </summary>
        ''' <remarks>
        ''' This pointer must be resolved before the field is written to the schema table.
        ''' It is only meaningful when the base type of <see cref="F.TypeCode"/> is Object.
        ''' </remarks>
        Public RefSchemaPtr As PTR

        ''' <summary>
        ''' Creates a session field wrapper for the specified global field definition.
        ''' </summary>
        ''' <param name="f">Global field metadata.</param>
        Public Sub New(f As JsonField)
            Me.F = f
        End Sub

#Region "WriteToStream"

        ''' <summary>
        ''' Writes this field entry to the session schema table.
        ''' </summary>
        ''' <param name="ms">Destination stream.</param>
        ''' <param name="s">Current encoding session.</param>
        ''' <remarks>
        ''' Field layout:
        '''
        '''   [typeCode: 1 byte]
        '''   [fieldName: DSTR chunk produced by the session string table]
        '''   [refSchemaPtr: only when base type is Object]
        '''
        ''' The field name is always emitted through the session string mechanism so that
        ''' repeated names can reuse session-local pointers.
        ''' </remarks>
        Public Sub WriteToStream(ms As Stream, s As ISession)

            ms.WriteByte(F.TypeCode)

            With s.AddString(F.Name)
                ms.Write(.len)
                ms.Write(.data)
            End With

            If JsonField.FieldTypeIsObjectOrObjectArray(F.TypeCode) Then
                ms.Write(Me.RefSchemaPtr.len)
                ms.Write(Me.RefSchemaPtr.data)
            End If

        End Sub

#End Region

    End Class

End Namespace