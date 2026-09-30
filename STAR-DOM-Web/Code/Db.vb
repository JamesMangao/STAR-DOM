Imports System.Configuration
Imports Npgsql

Namespace STAR_DOM.Database

    ''' <summary>Core PostgreSQL/Supabase access for the web app. All SQL flows through here with parameters only.</summary>
    Public Module Db

        ''' <summary>Which backend this build talks to. Surfaced by /health for smoke tests.</summary>
        Public Const Provider As String = "Supabase/PostgreSQL (Npgsql)"

        ' Every primary key in the schema is a plain serial "Id", so the id-producing
        ' INSERTs can all be handled by one generic RETURNING clause.
        Private Const IdReturning As String = " RETURNING Id"

        ''' <summary>
        ''' Builds an Npgsql connection string from Supabase's environment variables.
        ''' Supabase hands out the database host as a sibling of the project URL
        ''' (project-ref.supabase.co becomes db.project-ref.supabase.co), so both
        ''' spellings are accepted.
        ''' </summary>
        Public Function ConnString() As String
            ' 1. A ready-made URI (Supabase's "connection string" / add-on databases).
            Dim uri As String = FirstEnv("SUPABASE_DB_URL", "POSTGRES_URL", "DATABASE_URL")
            If Not String.IsNullOrWhiteSpace(uri) Then
                Return uri
            End If

            Dim host As String = FirstEnv("SUPABASE_DB_HOST", "DB_HOST", "PGHOST")
            Dim pass As String = FirstEnv("SUPABASE_DB_PASSWORD", "SUPABASE_PASSWORD", "DB_PASSWORD", "PGPASSWORD")

            ' 2. Only SUPABASE_URL + SUPABASE_DB_PASSWORD: derive the database host from the project URL.
            If String.IsNullOrWhiteSpace(host) Then
                Dim projectUrl As String = FirstEnv("SUPABASE_URL")
                If Not String.IsNullOrWhiteSpace(projectUrl) AndAlso Not String.IsNullOrWhiteSpace(pass) Then
                    host = ProjectHost(projectUrl)
                End If
            End If

            If Not String.IsNullOrWhiteSpace(host) Then
                Dim port As String = FirstEnv("SUPABASE_DB_PORT", "DB_PORT", "PGPORT", "5432")
                Dim name As String = FirstEnv("SUPABASE_DB_NAME", "DB_NAME", "PGDATABASE", "postgres")
                Dim user As String = FirstEnv("SUPABASE_DB_USER", "DB_USER", "PGUSER", "postgres")
                Dim ssl As String = FirstEnv("DB_SSLMODE", "SSLMODE", "Require")
                Return String.Format(
                    "Host={0};Port={1};Database={2};Username={3};Password={4};SSL Mode={5};" &
                    "Timeout=15;Command Timeout=30;Pooling=true;Max Pool Size=50;" &
                    "Time Zone={6};Include Error Detail=true",
                    host, port, name, user, pass, ssl, AppTimeZone())
            End If

            ' 3. web.config (local development / self-hosted fallback).
            Dim csSetting = ConfigurationManager.ConnectionStrings("STAR_DOM")
            If csSetting IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(csSetting.ConnectionString) Then
                Return csSetting.ConnectionString
            End If

            ' 4. Local Postgres default.
            Return "Host=localhost;Port=5432;Database=stardom;Username=postgres;Password=postgres;" &
                   "SSL Mode=Disable;Timeout=15;Command Timeout=30;Time Zone=" & AppTimeZone()
        End Function

        ''' <summary>
        ''' Maps the Supabase project URL onto its Postgres endpoint:
        ''' project-ref.supabase.co becomes db.project-ref.supabase.co.
        ''' The project host does not speak the Postgres wire protocol; only the
        ''' db.* sibling does.
        ''' </summary>
        Private Function ProjectHost(projectUrl As String) As String
            Dim u As Uri = Nothing
            If Not Uri.TryCreate(projectUrl.Trim(), UriKind.Absolute, u) Then
                Return "localhost"
            End If
            Dim h As String = u.Host
            If h.StartsWith("www.", StringComparison.OrdinalIgnoreCase) Then
                h = h.Substring(4)
            End If
            If h.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase) Then
                h = "db." & h
            End If
            Return h
        End Function

        Private Function FirstEnv(ParamArray names() As String) As String
            For Each n As String In names
                Dim v As String = Environment.GetEnvironmentVariable(n)
                If Not String.IsNullOrWhiteSpace(v) Then Return v
            Next
            Return ""
        End Function

        ''' <summary>
        ''' Timestamps are stored as timestamptz but the UI presents Asia/Manila wall-clock
        ''' times. Asking Npgsql for that zone returns local DateTimes (Kind=Unspecified), which
        ''' keeps every Fmt.* call rendering exactly what the previous MySQL host produced.
        ''' </summary>
        Private Function AppTimeZone() As String
            Dim tz As String = Environment.GetEnvironmentVariable("APP_TIMEZONE")
            If String.IsNullOrWhiteSpace(tz) Then Return "Asia/Manila"
            Return tz
        End Function

        Public Function Ping() As Boolean
            Try
                Using conn As New NpgsqlConnection(ConnString())
                    conn.Open()
                    Return True
                End Using
            Catch
                Return False
            End Try
        End Function

        Public Function NewConnection() As NpgsqlConnection
            Return New NpgsqlConnection(ConnString())
        End Function

        Public Function OpenConnection() As NpgsqlConnection
            Dim c As NpgsqlConnection = NewConnection()
            c.Open()
            Return c
        End Function

        ''' <summary>Builds a named parameter. "@name" is valid in Npgsql as well as MySql.Data.</summary>
        Public Function P(name As String, value As Object) As NpgsqlParameter
            If value Is Nothing Then value = DBNull.Value
            Return New NpgsqlParameter(name, value)
        End Function

        ' ── Transactions ───────────────────────────────────────────────────────
        ' Multi-step writes used to auto-commit one statement at a time, so a failure
        ' halfway through left partial rows behind. InTransaction() pins every Db.* call
        ' made by the work delegate to ONE connection and ONE transaction, so the whole
        ' block either lands or leaves nothing.
        '
        ' The ambient state is [ThreadStatic] because ASP.NET serves many requests
        ' concurrently: a shared static would leak one request's transaction into
        ' another's. This application is entirely synchronous (no Async/Await anywhere),
        ' so the ambient state always stays on the thread that created it.
        ' (Module members are implicitly Shared already — do NOT add 'Shared'.)
        <ThreadStatic>
        Private _ambientConn As NpgsqlConnection

        <ThreadStatic>
        Private _ambientTx As NpgsqlTransaction

        ''' <summary>Run work atomically: one connection, one transaction, rollback on any throw.</summary>
        Public Function InTransaction(Of TResult)(work As Func(Of TResult)) As TResult
            ' Already inside a transaction (nested call): join it rather than opening a
            ' second connection, so the outer block still rolls everything back.
            If _ambientTx IsNot Nothing Then Return work()

            Dim conn As NpgsqlConnection = OpenConnection()
            Dim tx As NpgsqlTransaction = Nothing
            Dim committed As Boolean = False
            Try
                tx = conn.BeginTransaction()
                _ambientConn = conn
                _ambientTx = tx

                Dim result As TResult = work()

                tx.Commit()
                committed = True
                Return result
            Catch
                If Not committed AndAlso tx IsNot Nothing Then
                    Try
                        tx.Rollback()
                    Catch
                        ' best effort: the server may already have rolled back
                    End Try
                End If
                Throw
            Finally
                _ambientConn = Nothing
                _ambientTx = Nothing
                If tx IsNot Nothing Then tx.Dispose()
                conn.Dispose()
            End Try
        End Function

        ''' <summary>True while InTransaction() has an open transaction on this thread.</summary>
        Public ReadOnly Property InTransactionScope As Boolean
            Get
                Return _ambientTx IsNot Nothing
            End Get
        End Property

        ''' <summary>
        ''' The ambient connection when a transaction is open, otherwise a fresh one that
        ''' the caller owns and must dispose.
        ''' </summary>
        Private Function ResolveConnection(ByRef owned As Boolean) As NpgsqlConnection
            If _ambientConn IsNot Nothing Then
                owned = False
                Return _ambientConn
            End If
            owned = True
            Return OpenConnection()
        End Function

        Public Function Exec(sql As String, ParamArray ps() As NpgsqlParameter) As Integer
            Dim owned As Boolean = False
            Dim conn As NpgsqlConnection = ResolveConnection(owned)
            Try
                Using cmd As New NpgsqlCommand(sql, conn)
                    If _ambientTx IsNot Nothing Then cmd.Transaction = _ambientTx
                    If ps IsNot Nothing AndAlso ps.Length > 0 Then cmd.Parameters.AddRange(ps)
                    Return cmd.ExecuteNonQuery()
                End Using
            Finally
                If owned Then conn.Dispose()
            End Try
        End Function

        ''' <summary>
        ''' Run an INSERT and return the new serial id.
        ''' PostgreSQL has no LAST_INSERT_ID(); the id comes back from a RETURNING clause on
        ''' the INSERT itself, so it is read in the same round trip and can never be
        ''' contaminated by another session the way a pooled connection's value could be.
        ''' </summary>
        Public Function ExecIdentity(sql As String, ParamArray ps() As NpgsqlParameter) As Integer
            Dim statement As String = sql.TrimEnd()
            If statement.EndsWith(";") Then statement = statement.Substring(0, statement.Length - 1).TrimEnd()
            If statement.IndexOf("RETURNING", StringComparison.OrdinalIgnoreCase) < 0 Then
                statement &= IdReturning
            End If

            Dim owned As Boolean = False
            Dim conn As NpgsqlConnection = ResolveConnection(owned)
            Try
                Using cmd As New NpgsqlCommand(statement, conn)
                    If _ambientTx IsNot Nothing Then cmd.Transaction = _ambientTx
                    If ps IsNot Nothing AndAlso ps.Length > 0 Then cmd.Parameters.AddRange(ps)
                    Dim o As Object = cmd.ExecuteScalar()
                    If o Is Nothing OrElse o Is DBNull.Value Then Return 0
                    Return Convert.ToInt32(o)
                End Using
            Finally
                If owned Then conn.Dispose()
            End Try
        End Function

        Public Function ScalarInt(sql As String, ParamArray ps() As NpgsqlParameter) As Integer
            Dim o As Object = RawScalar(sql, ps)
            If o Is Nothing OrElse o Is DBNull.Value Then Return 0
            Return Convert.ToInt32(o)
        End Function

        Public Function ScalarStr(sql As String, ParamArray ps() As NpgsqlParameter) As String
            Dim o As Object = RawScalar(sql, ps)
            If o Is Nothing OrElse o Is DBNull.Value Then Return ""
            Return Convert.ToString(o)
        End Function

        Public Function ScalarDec(sql As String, ParamArray ps() As NpgsqlParameter) As Decimal
            Dim o As Object = RawScalar(sql, ps)
            If o Is Nothing OrElse o Is DBNull.Value Then Return 0D
            Return Convert.ToDecimal(o)
        End Function

        Public Function ScalarDate(sql As String, ParamArray ps() As NpgsqlParameter) As Date
            Dim o As Object = RawScalar(sql, ps)
            If o Is Nothing OrElse o Is DBNull.Value Then Return Date.MinValue
            Return Convert.ToDateTime(o)
        End Function

        Private Function RawScalar(sql As String, ps() As NpgsqlParameter) As Object
            Dim owned As Boolean = False
            Dim conn As NpgsqlConnection = ResolveConnection(owned)
            Try
                Using cmd As New NpgsqlCommand(sql, conn)
                    If _ambientTx IsNot Nothing Then cmd.Transaction = _ambientTx
                    If ps IsNot Nothing AndAlso ps.Length > 0 Then cmd.Parameters.AddRange(ps)
                    Return cmd.ExecuteScalar()
                End Using
            Finally
                If owned Then conn.Dispose()
            End Try
        End Function

        Public Function Query(sql As String, ParamArray ps() As NpgsqlParameter) As DataTable
            Dim dt As New DataTable()
            Dim owned As Boolean = False
            Dim conn As NpgsqlConnection = ResolveConnection(owned)
            Try
                Using cmd As New NpgsqlCommand(sql, conn)
                    If _ambientTx IsNot Nothing Then cmd.Transaction = _ambientTx
                    If ps IsNot Nothing AndAlso ps.Length > 0 Then cmd.Parameters.AddRange(ps)
                    Using da As New NpgsqlDataAdapter(cmd)
                        da.Fill(dt)
                    End Using
                End Using
            Finally
                If owned Then conn.Dispose()
            End Try
            Return dt
        End Function

        Public Function Rows(sql As String, ParamArray ps() As NpgsqlParameter) As List(Of DataRow)
            Dim dt As DataTable = Query(sql, ps)
            Return dt.Rows.Cast(Of DataRow)().ToList()
        End Function

        ''' <summary>Log a technical error to the AppErrors table (created on demand).</summary>
        Public Sub LogError(context As String, ex As Exception)
            Try
                Exec("CREATE TABLE IF NOT EXISTS AppErrors (" &
                     "Id SERIAL PRIMARY KEY, Context VARCHAR(100), " &
                     "Message TEXT, StackTrace TEXT, CreatedAt TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP)")
                Exec("CREATE INDEX IF NOT EXISTS IDX_AppErrors_Created ON AppErrors (CreatedAt)")
                Exec("INSERT INTO AppErrors (Context, Message, StackTrace) VALUES (@c, @m, @s)",
                     P("@c", context), P("@m", ex.Message), P("@s", ex.ToString()))
            Catch
                ' never let logging break the app
            End Try
        End Sub

        ''' <summary>Trim old error-log rows so AppErrors never grows without bound.</summary>
        Public Sub PruneErrors(Optional days As Integer = 30)
            Try
                Exec("DELETE FROM AppErrors WHERE CreatedAt < NOW() - make_interval(days => @d)", P("@d", days))
            Catch
                ' best effort; never break startup
            End Try
        End Sub

    End Module

    ''' <summary>Safe typed readers over a DataRow (handles DBNull everywhere).</summary>
    Public Module RowReader

        Public Function AsInt(row As DataRow, col As String, Optional def As Integer = 0) As Integer
            If row Is Nothing OrElse Not row.Table.Columns.Contains(col) Then Return def
            Dim o As Object = row(col)
            If o Is Nothing OrElse o Is DBNull.Value Then Return def
            Try
                Return Convert.ToInt32(o)
            Catch
                Return def
            End Try
        End Function

        Public Function AsStr(row As DataRow, col As String, Optional def As String = "") As String
            If row Is Nothing OrElse Not row.Table.Columns.Contains(col) Then Return def
            Dim o As Object = row(col)
            If o Is Nothing OrElse o Is DBNull.Value Then Return def
            Return Convert.ToString(o)
        End Function

        Public Function AsDec(row As DataRow, col As String, Optional def As Decimal = 0D) As Decimal
            If row Is Nothing OrElse Not row.Table.Columns.Contains(col) Then Return def
            Dim o As Object = row(col)
            If o Is Nothing OrElse o Is DBNull.Value Then Return def
            Try
                Return Convert.ToDecimal(o)
            Catch
                Return def
            End Try
        End Function

        Public Function AsDate(row As DataRow, col As String) As Date
            If row Is Nothing OrElse Not row.Table.Columns.Contains(col) Then Return Date.MinValue
            Dim o As Object = row(col)
            If o Is Nothing OrElse o Is DBNull.Value Then Return Date.MinValue
            Try
                Return Convert.ToDateTime(o)
            Catch
                Return Date.MinValue
            End Try
        End Function

        Public Function AsNullableDate(row As DataRow, col As String) As Date?
            If row Is Nothing OrElse Not row.Table.Columns.Contains(col) Then Return Nothing
            Dim o As Object = row(col)
            If o Is Nothing OrElse o Is DBNull.Value Then Return Nothing
            Try
                Return Convert.ToDateTime(o)
            Catch
                Return Nothing
            End Try
        End Function

        Public Function AsNullableInt(row As DataRow, col As String) As Integer?
            If row Is Nothing OrElse Not row.Table.Columns.Contains(col) Then Return Nothing
            Dim o As Object = row(col)
            If o Is Nothing OrElse o Is DBNull.Value Then Return Nothing
            Try
                Return Convert.ToInt32(o)
            Catch
                Return Nothing
            End Try
        End Function

        Public Function AsNullableDec(row As DataRow, col As String) As Decimal?
            If row Is Nothing OrElse Not row.Table.Columns.Contains(col) Then Return Nothing
            Dim o As Object = row(col)
            If o Is Nothing OrElse o Is DBNull.Value Then Return Nothing
            Try
                Return Convert.ToDecimal(o)
            Catch
                Return Nothing
            End Try
        End Function

        ''' <summary>
        ''' Accepts both a real boolean (PostgreSQL BOOLEAN) and the 0/1 that the retired
        ''' MySQL TINYINT(1) columns produced, so either backend shape reads correctly.
        ''' </summary>
        Public Function AsBool(row As DataRow, col As String) As Boolean
            If row Is Nothing OrElse Not row.Table.Columns.Contains(col) Then Return False
            Dim o As Object = row(col)
            If o Is Nothing OrElse o Is DBNull.Value Then Return False
            Try
                If TypeOf o Is Boolean Then Return CBool(o)
                Return Convert.ToInt32(o) <> 0
            Catch
                Return False
            End Try
        End Function

    End Module

End Namespace
