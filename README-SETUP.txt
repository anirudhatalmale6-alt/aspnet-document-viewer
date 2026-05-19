================================================================================
  DOCUMENT VIEWER - SETUP & DEPLOYMENT GUIDE
================================================================================

  A standalone ASP.NET Web Forms page that browses a Windows shared folder
  and lets users view or download documents through the browser.

  Requirements: .NET Framework 4.7.2+, IIS 7+, no NuGet packages needed.

================================================================================
  1. ADDING TO AN EXISTING WEB FORMS PROJECT
================================================================================

  Copy these files into your project:

    DocumentViewer.aspx       - The page markup (HTML + embedded CSS)
    DocumentViewer.aspx.cs    - The C# code-behind

  The page uses namespace "DocumentViewer" -- if your project has a different
  root namespace, update the namespace in the .cs file and the Inherits
  attribute in the .aspx directive to match.

  The web.config settings (appSettings, httpRuntime limits) should be merged
  into your existing web.config. Do not overwrite your web.config entirely.

  No NuGet packages, external CSS, or JavaScript libraries are required.
  Everything is self-contained.


================================================================================
  2. WEB.CONFIG SETTINGS
================================================================================

  <appSettings>
    <add key="SharedFolderPath" value="\\FileServer\SharedDocs" />
  </appSettings>

  SharedFolderPath
  ----------------
  The UNC path (or local path) to the root folder you want to browse.

  Examples:
    \\FileServer\SharedDocs           UNC path to a Windows share
    \\192.168.1.100\CompanyDocs       UNC path by IP
    C:\inetpub\SharedFiles            Local folder on the web server

  IMPORTANT: Use the full UNC path, not a mapped drive letter (Z:\Docs).
  Mapped drives are per-user and are not available to IIS service accounts.


================================================================================
  3. IIS APP POOL IDENTITY & PERMISSIONS
================================================================================

  The IIS application pool identity must have READ permission on the shared
  folder. There are two approaches:

  OPTION A: App Pool Identity (Recommended)
  ------------------------------------------

  1. In IIS Manager, find your Application Pool.
  2. Note the Identity (e.g., "ApplicationPoolIdentity" or a custom account).
  3. On the file server, grant READ & LIST permission to:

     For ApplicationPoolIdentity:
       IIS AppPool\<YourPoolName>     (local shares on the same server)
       COMPUTERNAME$                   (network shares -- use the machine
                                        account of the web server)

     For a custom account (e.g., DOMAIN\WebAppSvc):
       Grant that account read access on the share and NTFS.

  4. Both SHARE permissions and NTFS permissions must allow read access.

  OPTION B: Explicit Impersonation
  ---------------------------------

  If you need a specific service account to access the share, enable
  impersonation in web.config:

    <system.web>
      <identity impersonate="true"
                userName="DOMAIN\ServiceAccount"
                password="P@ssw0rd" />
    </system.web>

  The specified account must have read access to the share. This approach
  is useful when the web server and file server are in different domains
  or when you cannot grant the machine account access.

  SECURITY NOTE: Storing passwords in web.config is a risk. Consider
  encrypting the <identity> section using aspnet_regiis -pe or use
  Windows Credential Manager / a vault solution.


================================================================================
  4. REQUEST SIZE LIMITS (LARGE FILE DOWNLOADS)
================================================================================

  The page streams files to the browser in 64 KB chunks, so server memory
  usage stays low even for large files. However, IIS and ASP.NET have
  default request/response size limits:

  In web.config:

    <!-- ASP.NET limit (in KB). 2097152 KB = 2 GB -->
    <httpRuntime maxRequestLength="2097152" executionTimeout="3600" />

    <!-- IIS limit (in bytes). 2147483648 = 2 GB -->
    <requestFiltering>
      <requestLimits maxAllowedContentLength="2147483648" />
    </requestFiltering>

  Adjust these values based on the largest files in your share.


================================================================================
  5. SUPPORTED FILE TYPES
================================================================================

  The viewer recognizes these file types with colored icons:

    PDF (.pdf)                          Red icon, viewable in browser
    Excel (.xls, .xlsx, .xlsm, .csv)   Green icon
    Word (.doc, .docx, .rtf, .odt)     Blue icon
    PowerPoint (.ppt, .pptx)           Orange icon
    Email (.msg, .eml)                 Purple envelope icon
    Images (.jpg, .png, .gif, .bmp,    Teal landscape icon,
            .svg, .webp, .tif)          viewable in browser
    Text (.txt, .log, .md, .json,      Gray icon,
          .xml, .htm, .html)            viewable in browser
    Archives (.zip, .rar, .7z, .gz)    Amber zipper icon
    Audio (.mp3, .wav, .flac)          Purple icon
    Video (.mp4, .avi, .mov)           Red icon
    Other                               Gray generic icon

  "Viewable in browser" types open inline (Content-Disposition: inline).
  All other types trigger a download. Every file also has a Download button.


================================================================================
  6. SECURITY NOTES
================================================================================

  - Path traversal protection: The code strips ".." segments and validates
    that every resolved path stays within the configured SharedFolderPath
    using Path.GetFullPath comparison.

  - The page does NOT require authentication by default. Add your own
    <authorization> rules or integrate with Windows Authentication / Forms
    Authentication as needed.

  - Hidden files and folders (FileAttributes.Hidden) are excluded from
    listings.

  - File names in Content-Disposition headers are sanitized to prevent
    header injection.


================================================================================
  7. QUICK TEST
================================================================================

  1. Set SharedFolderPath to a local test folder:
       <add key="SharedFolderPath" value="C:\TestDocs" />

  2. Create C:\TestDocs and drop some sample files in it (PDFs, images,
     Word docs, etc.). Create a subfolder or two.

  3. Grant the IIS App Pool identity read access to C:\TestDocs.

  4. Browse to: http://localhost/YourApp/DocumentViewer.aspx

  5. You should see the file grid with icons. Click files to view/download.
     Click folders to navigate into them. Use the breadcrumb to go back.
     Type in the search box to filter. Change the sort dropdown to reorder.


================================================================================
  8. TROUBLESHOOTING
================================================================================

  "Folder not found" error:
    - Verify SharedFolderPath is correct and the folder exists.
    - If UNC, ensure the web server can reach the file server (ping test).
    - Ensure the share name is correct (case-sensitive on some systems).

  "Access Denied" error:
    - Check both SHARE and NTFS permissions for the App Pool identity.
    - For UNC paths, the web server machine account (SERVERNAME$) or the
      impersonated account must have read access.
    - Run "whoami" from the app to verify the effective identity:
        Response.Write(System.Security.Principal.WindowsIdentity.GetCurrent().Name);

  Files not displaying correctly:
    - Check that the MIME type is registered in IIS for the file extension.
    - For PDFs, ensure the browser has a PDF viewer (most modern browsers do).

  Large files timing out:
    - Increase executionTimeout in httpRuntime.
    - Increase maxAllowedContentLength in requestFiltering.


================================================================================
  END OF SETUP GUIDE
================================================================================
