'***********************************************************************
'  PhasorDefinition.vb - Phasor definition
'  Copyright � 2004 - TVA, all rights reserved
'
'  Build Environment: VB.NET, Visual Studio 2003
'  Primary Developer: James R Carroll, System Analyst [WESTAFF]
'      Office: COO - TRNS/PWR ELEC SYS O, CHATTANOOGA, TN - MR 2W-C
'       Phone: 423/751-2827
'       Email: jrcarrol@tva.gov
'
'  Code Modification History:
'  ---------------------------------------------------------------------
'  11/12/2004 - James R Carroll
'       Initial version of source generated
'
'***********************************************************************

Imports System.Text
Imports TVA.Interop
Imports TVA.Shared.Bit

Namespace EE.Phasor.IEEE1344

    Public Class DigitalDefinitions

        Inherits CollectionBase

        Friend Sub New()
        End Sub

        Public Sub Add(ByVal value As DigitalDefinition)

            List.Add(value)

        End Sub

        Default Public ReadOnly Property Item(ByVal index As Integer) As DigitalDefinition
            Get
                Return DirectCast(List.Item(index), DigitalDefinition)
            End Get
        End Property

    End Class

    Public Class DigitalDefinition

        Public Const BinaryLength As Integer = 2

        Protected m_label As String
        Protected m_flags As Int16

        Public Sub New()

            m_label = ""

        End Sub

        Public Sub New(ByVal label As String, ByVal binaryImage As Byte(), ByVal startIndex As Integer)

            Dim buffer As Byte() = Array.CreateInstance(GetType(Byte), BinaryLength)

            m_label = label

            ' Get digital flags
            m_flags = EndianOrder.ReverseToInt16(binaryImage, startIndex)

        End Sub

        Public Property NormalState() As Byte
            Get
                Return m_flags And Bit4
            End Get
            Set(ByVal Value As Byte)
                If Value > 0 Then
                    m_flags = m_flags Or Bit4
                Else
                    m_flags = m_flags And Not Bit4
                End If
            End Set
        End Property

        Public Property InputNormalState() As Byte
            Get
                Return m_flags And Bit0
            End Get
            Set(ByVal Value As Byte)
                If Value > 0 Then
                    m_flags = m_flags Or Bit0
                Else
                    m_flags = m_flags And Not Bit0
                End If
            End Set
        End Property

        Public Property Label() As String
            Get
                Return m_label
            End Get
            Set(ByVal Value As String)
                If Len(Value) > MaximumLabelLength Then
                    Throw New OverflowException("Label length cannot exceed " & MaximumLabelLength)
                Else
                    m_label = Value
                End If
            End Set
        End Property

        Public ReadOnly Property MaximumLabelLength()
            Get
                Return 16
            End Get
        End Property

        Public ReadOnly Property LabelImage() As Byte()
            Get
                Return Encoding.ASCII.GetBytes(m_label.PadRight(MaximumLabelLength))
            End Get
        End Property

        Public ReadOnly Property BinaryImage() As Byte()
            Get
                Dim buffer As Byte() = Array.CreateInstance(GetType(Byte), BinaryLength)

                EndianOrder.SwapCopy(BitConverter.GetBytes(m_flags), 0, buffer, 0, 2)

                Return buffer
            End Get
        End Property

    End Class

End Namespace
