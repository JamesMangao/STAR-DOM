Imports System.Globalization

Namespace STAR_DOM.Helpers

    ''' <summary>
    ''' Wall-clock time source for the STAR:DOM platform, pinned to Asia/Manila (UTC+8, no DST).
    ''' PostgreSQL's TimeZone setting depends on the host, so "now" is computed here in
    ''' .NET and passed into SQL as a plain wall-clock parameter.
    ''' </summary>
    Public Module Clock

        Public Const TimeZoneId As String = "Asia/Manila"
        Private ReadOnly _tz As TimeZoneInfo

        Sub New()
            Try
                _tz = TimeZoneInfo.FindSystemTimeZoneById("Philippine Standard Time")
            Catch
                _tz = TimeZoneInfo.CreateCustomTimeZone("Philippine Standard Time", TimeSpan.FromHours(8), "PST", "PST")
            End Try
        End Sub

        Public ReadOnly Property Zone As TimeZoneInfo
            Get
                Return _tz
            End Get
        End Property

        ''' <summary>Current Asia/Manila wall-clock time.</summary>
        Public ReadOnly Property Now As Date
            Get
                Return TimeZoneInfo.ConvertTimeFromUtc(Date.UtcNow, _tz)
            End Get
        End Property

        ''' <summary>"2026-09-08 14:30:00" (yyyy-MM-dd HH:mm:ss) string safe to pass to a PostgreSQL TIMESTAMPTZ parameter.</summary>
        Public Function SqlNow() As String
            Return Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        End Function

        ''' <summary>Parse "10:00 AM" style opening/closing times into a TimeOfDay. Returns Nothing if unparseable.</summary>
        Public Function ParseTimeOfDay(value As String) As TimeSpan?
            If String.IsNullOrWhiteSpace(value) Then Return Nothing
            Dim d As Date
            If Date.TryParseExact(value.Trim(), {"h:mm tt", "h tt", "h:mmtt", "htt"}, CultureInfo.GetCultureInfo("en-US"),
                                  DateTimeStyles.None, d) Then
                Return d.TimeOfDay
            End If
            If Date.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, d) Then
                Return d.TimeOfDay
            End If
            Return Nothing
        End Function

        ''' <summary>Human-friendly relative label ("10s ago", "14m ago", "2h ago") against Manila time.</summary>
        Public Function RelativeTime(stamp As Date) As String
            Dim span As TimeSpan = Now - stamp
            If span.TotalSeconds < 10 Then Return "just now"
            If span.TotalMinutes < 60 Then Return CInt(Math.Floor(span.TotalMinutes)).ToString() & "m ago"
            If span.TotalHours < 24 Then Return CInt(Math.Floor(span.TotalHours)).ToString() & "h ago"
            Return CInt(Math.Floor(span.TotalDays)).ToString() & "d ago"
        End Function

    End Module

End Namespace