Imports STAR_DOM.Models
Imports STAR_DOM.Repositories

Namespace STAR_DOM.Services

    ''' <summary>
    ''' Customer-facing view of the admin-managed payment channels.
    ''' </summary>
    ''' <remarks>
    ''' Payment Settings lets an admin create any channel (GCash, GOtyme, and
    ''' whatever else they add). Anything that has to show those channels to a
    ''' customer reads them through here rather than through the repository, so the
    ''' page -> service -> repository layering in ARCHITECTURE-DIAGRAM.md still
    ''' holds: the master page footer included.
    ''' </remarks>
    Public Class PaymentSettingService

        Private ReadOnly _repo As New PaymentSettingRepository()

        ''' <summary>
        ''' Channel keys the storefront should advertise, built-ins first, admin-created
        ''' channels after them, alphabetically. Empty when nothing is configured.
        ''' </summary>
        Public Function EnabledChannels() As List(Of String)
            Return _repo.ListAll().
                Where(Function(p) p.IsEnabled).
                Select(Function(p) p.Channel).
                ToList()
        End Function

    End Class

End Namespace
