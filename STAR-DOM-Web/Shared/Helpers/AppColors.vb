Imports System.Drawing

Namespace STAR_DOM.Helpers

    ''' <summary>
    ''' STAR:DOM design tokens (from DESIGN.md). All WinForms colors come from here
    ''' so the entire application keeps one visual language.
    ''' </summary>
    Public Module AppColors

        ' --- Neutral surfaces -------------------------------------------------
        Public ReadOnly Surface As Color = ColorTranslator.FromHtml("#FFF8F5")        ' page background
        Public ReadOnly SurfaceDim As Color = ColorTranslator.FromHtml("#E0D8D5")
        Public ReadOnly SurfaceContainerLowest As Color = ColorTranslator.FromHtml("#FFFFFF") ' cards / modals
        Public ReadOnly SurfaceContainerLow As Color = ColorTranslator.FromHtml("#FAF2EE")
        Public ReadOnly SurfaceContainer As Color = ColorTranslator.FromHtml("#F4ECE8")
        Public ReadOnly SurfaceContainerHigh As Color = ColorTranslator.FromHtml("#EEE7E3")
        Public ReadOnly SurfaceContainerHighest As Color = ColorTranslator.FromHtml("#E9E1DD")
        Public ReadOnly OnSurface As Color = ColorTranslator.FromHtml("#1E1B19")
        Public ReadOnly OnSurfaceVariant As Color = ColorTranslator.FromHtml("#5C403C")
        Public ReadOnly Outline As Color = ColorTranslator.FromHtml("#916F6B")
        Public ReadOnly OutlineVariant As Color = ColorTranslator.FromHtml("#E6BDB8")
        Public ReadOnly InverseSurface As Color = ColorTranslator.FromHtml("#33302D")
        Public ReadOnly InverseOnSurface As Color = ColorTranslator.FromHtml("#F7EFEB")

        ' --- Brand -----------------------------------------------------------
        Public ReadOnly Primary As Color = ColorTranslator.FromHtml("#B70011")        ' deep crimson
        Public ReadOnly OnPrimary As Color = ColorTranslator.FromHtml("#FFFFFF")
        Public ReadOnly PrimaryContainer As Color = ColorTranslator.FromHtml("#DC2626")
        Public ReadOnly OnPrimaryContainer As Color = ColorTranslator.FromHtml("#FFF6F5")
        Public ReadOnly PrimaryFixed As Color = ColorTranslator.FromHtml("#FFDAD6")
        Public ReadOnly PrimaryFixedDim As Color = ColorTranslator.FromHtml("#FFB4AB")

        Public ReadOnly Secondary As Color = ColorTranslator.FromHtml("#735C00")      ' electric gold family
        Public ReadOnly OnSecondary As Color = ColorTranslator.FromHtml("#FFFFFF")
        Public ReadOnly SecondaryContainer As Color = ColorTranslator.FromHtml("#FED01B")
        Public ReadOnly OnSecondaryContainer As Color = ColorTranslator.FromHtml("#6F5900")
        Public ReadOnly SecondaryFixed As Color = ColorTranslator.FromHtml("#FFE083")
        Public ReadOnly SecondaryFixedDim As Color = ColorTranslator.FromHtml("#EEC200")
        Public ReadOnly OnSecondaryFixed As Color = ColorTranslator.FromHtml("#231B00")
        Public ReadOnly OnSecondaryFixedVariant As Color = ColorTranslator.FromHtml("#574500")

        Public ReadOnly Tertiary As Color = ColorTranslator.FromHtml("#954000")       ' cadmium orange
        Public ReadOnly OnTertiary As Color = ColorTranslator.FromHtml("#FFFFFF")
        Public ReadOnly TertiaryContainer As Color = ColorTranslator.FromHtml("#BC5200")
        Public ReadOnly OnTertiaryContainer As Color = ColorTranslator.FromHtml("#FFF6F3")
        Public ReadOnly TertiaryFixed As Color = ColorTranslator.FromHtml("#FFDBCA")
        Public ReadOnly TertiaryFixedDim As Color = ColorTranslator.FromHtml("#FFB690")

        Public ReadOnly ErrorColor As Color = ColorTranslator.FromHtml("#BA1A1A")
        Public ReadOnly OnError As Color = ColorTranslator.FromHtml("#FFFFFF")
        Public ReadOnly ErrorContainer As Color = ColorTranslator.FromHtml("#FFDAD6")
        Public ReadOnly OnErrorContainer As Color = ColorTranslator.FromHtml("#93000A")

        ' --- Typography helpers ----------------------------------------------
        Public ReadOnly FontBody As String = "Segoe UI"
        Public ReadOnly FontHeadline As String = "Segoe UI Semibold"

        ' Convenience shortcut for primary-action red.
        Public ReadOnly Cta As Color = PrimaryContainer

    End Module

End Namespace