'*******************************************************************************************************
'  Tva.Interop.Windows.Common.vb - Common Windows API Related Functions
'  Copyright � 2005 - TVA, all rights reserved - Gbtc
'
'  Build Environment: VB.NET, Visual Studio 2005
'  Primary Developer: James R Carroll, Operations Data Architecture [TVA]
'      Office: COO - TRNS/PWR ELEC SYS O, CHATTANOOGA, TN - MR 2W-C
'       Phone: 423/751-2827
'       Email: jrcarrol@tva.gov
'
'  Code Modification History:
'  -----------------------------------------------------------------------------------------------------
'  01/24/2006 - James R Carroll
'       Initial version of source created
'
'*******************************************************************************************************

Imports System.Runtime.InteropServices

Namespace IO.Interop.Windows

    ''' <summary>
    ''' <para>Defines common Windows API related functions</para>
    ''' </summary>
    Public NotInheritable Class Common

        <DllImport("kernel32.dll")> _
        Private Shared Function FormatMessage(ByVal dwFlags As Integer, ByRef lpSource As IntPtr, ByVal dwMessageId As Integer, ByVal dwLanguageId As Integer, ByRef lpBuffer As String, ByVal nSize As Integer, ByRef Arguments As IntPtr) As Integer
        End Function

        ''' <summary>
        ''' Formats and returns a Windows API level error message corresponding to the specified error code
        ''' </summary>
        Public Shared Function GetErrorMessage(ByVal errorCode As Integer) As String

            Const FORMAT_MESSAGE_ALLOCATE_BUFFER As Integer = &H100
            Const FORMAT_MESSAGE_IGNORE_INSERTS As Integer = &H200
            Const FORMAT_MESSAGE_FROM_SYSTEM As Integer = &H1000

            Dim messageSize As Integer = 255
            Dim lpMsgBuf As String = ""
            Dim dwFlags As Integer = FORMAT_MESSAGE_ALLOCATE_BUFFER Or FORMAT_MESSAGE_FROM_SYSTEM Or FORMAT_MESSAGE_IGNORE_INSERTS
            Dim ptrlpSource As IntPtr = IntPtr.Zero
            Dim prtArguments As IntPtr = IntPtr.Zero

            If FormatMessage(dwFlags, ptrlpSource, errorCode, 0, lpMsgBuf, messageSize, prtArguments) = 0 Then
                Throw New InvalidOperationException("Failed to format message for error code " & errorCode)
            End If

            Return lpMsgBuf

        End Function

    End Class

End Namespace
