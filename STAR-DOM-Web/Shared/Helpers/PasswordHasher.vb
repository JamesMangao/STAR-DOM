Imports System.Security.Cryptography
Imports System.Text

Namespace STAR_DOM.Helpers

    ''' <summary>
    ''' Password hashing using PBKDF2-HMAC-SHA256 (10,000 iterations).
    ''' Stored format: PBKDF2$10000$&lt;base64 salt&gt;$&lt;base64 hash&gt;
    ''' </summary>
    Public Module PasswordHasher

        Private Const Iterations As Integer = 10000
        Private Const SaltSize As Integer = 16
        Private Const HashSize As Integer = 32

        Public Function Hash(password As String) As String
            Dim salt As Byte() = New Byte(SaltSize - 1) {}
            Using rng As New RNGCryptoServiceProvider()
                rng.GetBytes(salt)
            End Using
            Dim derived As Byte() = Derive(password, salt)
            Return "PBKDF2$" & Iterations.ToString() & "$" & Convert.ToBase64String(salt) & "$" & Convert.ToBase64String(derived)
        End Function

        Public Function Verify(password As String, stored As String) As Boolean
            If String.IsNullOrEmpty(stored) Then Return False
            Dim parts As String() = stored.Split("$"c)
            If parts.Length <> 4 Then Return False
            If Not String.Equals(parts(0), "PBKDF2", StringComparison.OrdinalIgnoreCase) Then Return False

            Dim iters As Integer
            If Not Integer.TryParse(parts(1), iters) Then Return False
            Dim salt As Byte()
            Dim expected As Byte()
            Try
                salt = Convert.FromBase64String(parts(2))
                expected = Convert.FromBase64String(parts(3))
            Catch
                Return False
            End Try

            Dim actual As Byte() = Derive(password, salt, iters)
            Return FixedTimeEquals(actual, expected)
        End Function

        Private Function Derive(password As String, salt As Byte(), Optional iterations As Integer = Iterations) As Byte()
            Using pbkdf2 As New Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256)
                Return pbkdf2.GetBytes(HashSize)
            End Using
        End Function

        Private Function FixedTimeEquals(a As Byte(), b As Byte()) As Boolean
            If a.Length <> b.Length Then Return False
            Dim diff As Integer = 0
            For i As Integer = 0 To a.Length - 1
                diff = diff Or (CInt(a(i)) Xor CInt(b(i)))
            Next
            Return diff = 0
        End Function

    End Module

End Namespace