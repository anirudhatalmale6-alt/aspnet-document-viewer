using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;

namespace DocumentViewer
{
    public partial class DocumentViewerPage : Page
    {
        // ------------------------------------------------------------------
        //  Configuration helpers
        // ------------------------------------------------------------------

        private string SharedFolderRoot
        {
            get
            {
                string path = ConfigurationManager.AppSettings["SharedFolderPath"];
                if (string.IsNullOrWhiteSpace(path))
                    throw new InvalidOperationException("AppSetting 'SharedFolderPath' is not configured.");
                return path.TrimEnd('\\', '/');
            }
        }

        /// <summary>Current relative sub-path inside the share (from query string).</summary>
        private string CurrentRelativePath
        {
            get
            {
                string raw = Request.QueryString["path"] ?? string.Empty;
                return SanitizeRelativePath(raw);
            }
        }

        private string CurrentFullPath => Path.Combine(SharedFolderRoot, CurrentRelativePath);

        // ------------------------------------------------------------------
        //  Page lifecycle
        // ------------------------------------------------------------------

        protected void Page_Load(object sender, EventArgs e)
        {
            string action = Request.QueryString["action"];

            // Serve file requests (view / download)
            if (!string.IsNullOrEmpty(action))
            {
                HandleFileAction(action);
                return;
            }

            if (!IsPostBack)
            {
                BindGrid();
            }
        }

        protected void txtSearch_TextChanged(object sender, EventArgs e) => BindGrid();
        protected void ddlSort_SelectedIndexChanged(object sender, EventArgs e) => BindGrid();

        // ------------------------------------------------------------------
        //  Grid binding
        // ------------------------------------------------------------------

        private void BindGrid()
        {
            string fullPath = CurrentFullPath;
            string relativePath = CurrentRelativePath;

            // Breadcrumb
            litBreadcrumb.Text = BuildBreadcrumbHtml(relativePath);

            // Validate directory
            if (!Directory.Exists(fullPath))
            {
                litGrid.Text = ErrorHtml("Folder not found", "The requested folder does not exist or is not accessible.");
                litItemCount.Text = "";
                return;
            }

            try
            {
                var di = new DirectoryInfo(fullPath);
                string filter = (txtSearch.Text ?? "").Trim();

                // --- Subfolders ---
                var folders = di.GetDirectories()
                    .Where(d => (d.Attributes & FileAttributes.Hidden) == 0)
                    .Where(d => string.IsNullOrEmpty(filter) ||
                                d.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                // --- Files ---
                var files = di.GetFiles()
                    .Where(f => (f.Attributes & FileAttributes.Hidden) == 0)
                    .Where(f => string.IsNullOrEmpty(filter) ||
                                f.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();

                // Sort
                string sort = ddlSort.SelectedValue ?? "name_asc";
                folders = SortFolders(folders, sort);
                files = SortFiles(files, sort);

                int totalItems = folders.Count + files.Count;
                litItemCount.Text = totalItems == 0 ? "No items" :
                    $"{totalItems} item{(totalItems == 1 ? "" : "s")} ({folders.Count} folder{(folders.Count == 1 ? "" : "s")}, {files.Count} file{(files.Count == 1 ? "" : "s")})";

                if (totalItems == 0)
                {
                    litGrid.Text = EmptyHtml(string.IsNullOrEmpty(filter) ? "This folder is empty" : "No results",
                        string.IsNullOrEmpty(filter) ? "There are no files or folders here." : "No items match your search query.");
                    return;
                }

                var sb = new StringBuilder();
                sb.Append("<div class=\"file-grid\">");

                // Render folders
                foreach (var folder in folders)
                {
                    string folderRel = string.IsNullOrEmpty(relativePath)
                        ? folder.Name
                        : relativePath + "\\" + folder.Name;
                    string navUrl = PageUrl(folderRel);
                    int subCount = 0;
                    try { subCount = folder.GetFileSystemInfos().Length; } catch { }

                    sb.Append("<div class=\"tile folder-tile\" onclick=\"window.location.href='")
                      .Append(HttpUtility.HtmlAttributeEncode(navUrl))
                      .Append("'\">");
                    sb.Append("<div class=\"tile-icon\">").Append(GetFolderSvg()).Append("</div>");
                    sb.Append("<div class=\"tile-name\">").Append(HttpUtility.HtmlEncode(folder.Name)).Append("</div>");
                    sb.Append("<div class=\"folder-badge\">").Append(subCount).Append(" item").Append(subCount == 1 ? "" : "s").Append("</div>");
                    sb.Append("</div>");
                }

                // Render files
                foreach (var file in files)
                {
                    string fileRel = string.IsNullOrEmpty(relativePath)
                        ? file.Name
                        : relativePath + "\\" + file.Name;

                    string viewUrl = FileActionUrl("view", fileRel);
                    string dlUrl = FileActionUrl("download", fileRel);
                    bool canView = CanViewInBrowser(file.Extension);

                    sb.Append("<div class=\"tile\">");
                    sb.Append("<div class=\"tile-icon\">");
                    if (canView)
                        sb.Append("<a href=\"").Append(HttpUtility.HtmlAttributeEncode(viewUrl)).Append("\" target=\"_blank\" title=\"View\">");
                    sb.Append(GetFileIconSvg(file.Extension));
                    if (canView)
                        sb.Append("</a>");
                    sb.Append("</div>");

                    sb.Append("<div class=\"tile-name\">");
                    if (canView)
                        sb.Append("<a href=\"").Append(HttpUtility.HtmlAttributeEncode(viewUrl)).Append("\" target=\"_blank\">");
                    sb.Append(HttpUtility.HtmlEncode(file.Name));
                    if (canView)
                        sb.Append("</a>");
                    sb.Append("</div>");

                    sb.Append("<div class=\"tile-meta\">");
                    sb.Append("<span>").Append(FormatFileSize(file.Length)).Append("</span>");
                    sb.Append("<span>").Append(file.LastWriteTime.ToString("MMM d, yyyy h:mm tt")).Append("</span>");
                    sb.Append("</div>");

                    sb.Append("<div class=\"tile-actions\">");
                    if (canView)
                    {
                        sb.Append("<a class=\"btn\" href=\"").Append(HttpUtility.HtmlAttributeEncode(viewUrl))
                          .Append("\" target=\"_blank\">")
                          .Append(EyeSvg()).Append(" View</a>");
                    }
                    sb.Append("<a class=\"btn").Append(canView ? "" : " btn-primary").Append("\" href=\"")
                      .Append(HttpUtility.HtmlAttributeEncode(dlUrl)).Append("\">")
                      .Append(DownloadSvg()).Append(" Download</a>");
                    sb.Append("</div>");

                    sb.Append("</div>");
                }

                sb.Append("</div>");
                litGrid.Text = sb.ToString();
            }
            catch (UnauthorizedAccessException)
            {
                litGrid.Text = ErrorHtml("Access Denied", "You do not have permission to access this folder. Check IIS App Pool identity permissions.");
                litItemCount.Text = "";
            }
            catch (Exception ex)
            {
                litGrid.Text = ErrorHtml("Error", HttpUtility.HtmlEncode(ex.Message));
                litItemCount.Text = "";
            }
        }

        // ------------------------------------------------------------------
        //  File serving (view / download)
        // ------------------------------------------------------------------

        private void HandleFileAction(string action)
        {
            string relPath = Request.QueryString["file"] ?? "";
            relPath = SanitizeRelativePath(relPath);

            if (string.IsNullOrEmpty(relPath))
            {
                Response.StatusCode = 400;
                Response.Write("Missing file parameter.");
                Response.End();
                return;
            }

            string fullPath = Path.Combine(SharedFolderRoot, relPath);

            // Security: ensure resolved path is within the share root
            string resolvedFull = Path.GetFullPath(fullPath);
            string resolvedRoot = Path.GetFullPath(SharedFolderRoot);
            if (!resolvedFull.StartsWith(resolvedRoot + "\\", StringComparison.OrdinalIgnoreCase) &&
                !resolvedFull.Equals(resolvedRoot, StringComparison.OrdinalIgnoreCase))
            {
                Response.StatusCode = 403;
                Response.Write("Access denied: path traversal detected.");
                Response.End();
                return;
            }

            if (!File.Exists(fullPath))
            {
                Response.StatusCode = 404;
                Response.Write("File not found.");
                Response.End();
                return;
            }

            try
            {
                var fi = new FileInfo(fullPath);
                string mimeType = GetMimeType(fi.Extension);
                string fileName = fi.Name;
                bool isView = action.Equals("view", StringComparison.OrdinalIgnoreCase);

                Response.Clear();
                Response.ContentType = mimeType;
                Response.AddHeader("Content-Length", fi.Length.ToString());

                if (isView && CanViewInBrowser(fi.Extension))
                {
                    Response.AddHeader("Content-Disposition", "inline; filename=\"" + CleanFileName(fileName) + "\"");
                }
                else
                {
                    Response.AddHeader("Content-Disposition", "attachment; filename=\"" + CleanFileName(fileName) + "\"");
                }

                // Stream the file in chunks to handle large files
                Response.Buffer = false;
                using (var fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    byte[] buffer = new byte[65536]; // 64 KB chunks
                    int bytesRead;
                    while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        if (!Response.IsClientConnected) break;
                        Response.OutputStream.Write(buffer, 0, bytesRead);
                        Response.Flush();
                    }
                }

                Response.End();
            }
            catch (UnauthorizedAccessException)
            {
                Response.StatusCode = 403;
                Response.Write("Access denied.");
                try { Response.End(); } catch { }
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                Response.Write("Error: " + HttpUtility.HtmlEncode(ex.Message));
                try { Response.End(); } catch { }
            }
        }

        // ------------------------------------------------------------------
        //  URL builders
        // ------------------------------------------------------------------

        private string PageUrl(string relativePath)
        {
            string page = Request.Url.AbsolutePath;
            if (string.IsNullOrEmpty(relativePath))
                return page;
            return page + "?path=" + HttpUtility.UrlEncode(relativePath.Replace("\\", "/"));
        }

        private string FileActionUrl(string action, string relativeFilePath)
        {
            string page = Request.Url.AbsolutePath;
            return page + "?action=" + HttpUtility.UrlEncode(action) +
                   "&file=" + HttpUtility.UrlEncode(relativeFilePath.Replace("\\", "/"));
        }

        // ------------------------------------------------------------------
        //  Breadcrumb
        // ------------------------------------------------------------------

        private string BuildBreadcrumbHtml(string relativePath)
        {
            var sb = new StringBuilder();

            // Home icon + root link
            sb.Append("<a href=\"").Append(HttpUtility.HtmlAttributeEncode(PageUrl(""))).Append("\" title=\"Root\">");
            sb.Append("<svg width=\"16\" height=\"16\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z\"/><polyline points=\"9 22 9 12 15 12 15 22\"/></svg>");
            sb.Append(" Root</a>");

            if (string.IsNullOrEmpty(relativePath)) return sb.ToString();

            string[] parts = relativePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            string accumulated = "";
            for (int i = 0; i < parts.Length; i++)
            {
                sb.Append("<span class=\"sep\">/</span>");
                accumulated = string.IsNullOrEmpty(accumulated) ? parts[i] : accumulated + "\\" + parts[i];

                if (i == parts.Length - 1)
                {
                    sb.Append("<span class=\"current\">").Append(HttpUtility.HtmlEncode(parts[i])).Append("</span>");
                }
                else
                {
                    sb.Append("<a href=\"").Append(HttpUtility.HtmlAttributeEncode(PageUrl(accumulated))).Append("\">");
                    sb.Append(HttpUtility.HtmlEncode(parts[i]));
                    sb.Append("</a>");
                }
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------
        //  Sorting
        // ------------------------------------------------------------------

        private List<DirectoryInfo> SortFolders(List<DirectoryInfo> list, string sort)
        {
            switch (sort)
            {
                case "name_desc": return list.OrderByDescending(d => d.Name).ToList();
                case "date_desc": return list.OrderByDescending(d => d.LastWriteTime).ToList();
                case "date_asc": return list.OrderBy(d => d.LastWriteTime).ToList();
                default: return list.OrderBy(d => d.Name).ToList();
            }
        }

        private List<FileInfo> SortFiles(List<FileInfo> list, string sort)
        {
            switch (sort)
            {
                case "name_desc": return list.OrderByDescending(f => f.Name).ToList();
                case "date_desc": return list.OrderByDescending(f => f.LastWriteTime).ToList();
                case "date_asc": return list.OrderBy(f => f.LastWriteTime).ToList();
                case "size_desc": return list.OrderByDescending(f => f.Length).ToList();
                case "size_asc": return list.OrderBy(f => f.Length).ToList();
                default: return list.OrderBy(f => f.Name).ToList();
            }
        }

        // ------------------------------------------------------------------
        //  Security helpers
        // ------------------------------------------------------------------

        /// <summary>
        /// Sanitize and normalize a relative path. Strips leading slashes, rejects
        /// any ".." segments, and normalizes separators.
        /// </summary>
        private string SanitizeRelativePath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            // Decode URL encoding
            raw = HttpUtility.UrlDecode(raw);

            // Normalize separators
            raw = raw.Replace("/", "\\");

            // Strip leading backslashes
            raw = raw.TrimStart('\\');

            // Reject path traversal
            string[] segments = raw.Split('\\');
            var clean = new List<string>();
            foreach (string seg in segments)
            {
                string trimmed = seg.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                if (trimmed == "." || trimmed == "..") continue; // strip traversal segments
                if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) continue; // skip invalid
                clean.Add(trimmed);
            }

            string result = string.Join("\\", clean);

            // Final safety check using Path.GetFullPath
            try
            {
                string resolvedFull = Path.GetFullPath(Path.Combine(SharedFolderRoot, result));
                string resolvedRoot = Path.GetFullPath(SharedFolderRoot);
                if (!resolvedFull.StartsWith(resolvedRoot, StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
            }
            catch
            {
                return string.Empty;
            }

            return result;
        }

        private string CleanFileName(string name)
        {
            // Remove characters that could break Content-Disposition header
            return name.Replace("\"", "'").Replace("\r", "").Replace("\n", "");
        }

        // ------------------------------------------------------------------
        //  File type helpers
        // ------------------------------------------------------------------

        private bool CanViewInBrowser(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return false;
            string ext = extension.ToLowerInvariant();
            return ext == ".pdf" || ext == ".txt" || ext == ".jpg" || ext == ".jpeg" ||
                   ext == ".png" || ext == ".gif" || ext == ".bmp" || ext == ".svg" ||
                   ext == ".webp" || ext == ".htm" || ext == ".html" || ext == ".xml" ||
                   ext == ".json" || ext == ".csv";
        }

        private string GetMimeType(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return "application/octet-stream";
            switch (extension.ToLowerInvariant())
            {
                // Documents
                case ".pdf": return "application/pdf";
                case ".doc": return "application/msword";
                case ".docx": return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                case ".xls": return "application/vnd.ms-excel";
                case ".xlsx": return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                case ".ppt": return "application/vnd.ms-powerpoint";
                case ".pptx": return "application/vnd.openxmlformats-officedocument.presentationml.presentation";
                case ".txt": return "text/plain";
                case ".csv": return "text/csv";
                case ".xml": return "application/xml";
                case ".json": return "application/json";
                case ".htm":
                case ".html": return "text/html";
                case ".rtf": return "application/rtf";

                // Images
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".png": return "image/png";
                case ".gif": return "image/gif";
                case ".bmp": return "image/bmp";
                case ".svg": return "image/svg+xml";
                case ".webp": return "image/webp";
                case ".ico": return "image/x-icon";
                case ".tif":
                case ".tiff": return "image/tiff";

                // Archives
                case ".zip": return "application/zip";
                case ".rar": return "application/x-rar-compressed";
                case ".7z": return "application/x-7z-compressed";
                case ".gz": return "application/gzip";
                case ".tar": return "application/x-tar";

                // Email
                case ".msg": return "application/vnd.ms-outlook";
                case ".eml": return "message/rfc822";

                // Media
                case ".mp3": return "audio/mpeg";
                case ".wav": return "audio/wav";
                case ".mp4": return "video/mp4";
                case ".avi": return "video/x-msvideo";
                case ".mov": return "video/quicktime";

                default: return "application/octet-stream";
            }
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1048576) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1073741824) return (bytes / 1048576.0).ToString("F1") + " MB";
            return (bytes / 1073741824.0).ToString("F2") + " GB";
        }

        // ------------------------------------------------------------------
        //  Status HTML
        // ------------------------------------------------------------------

        private string EmptyHtml(string title, string message)
        {
            return "<div class=\"empty-state\">" +
                   "<svg width=\"64\" height=\"64\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\">" +
                   "<path d=\"M22 19a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5l2 3h9a2 2 0 0 1 2 2z\"/></svg>" +
                   "<h3>" + HttpUtility.HtmlEncode(title) + "</h3>" +
                   "<p>" + HttpUtility.HtmlEncode(message) + "</p></div>";
        }

        private string ErrorHtml(string title, string message)
        {
            return "<div class=\"error-state\">" +
                   "<svg width=\"64\" height=\"64\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.5\" stroke-linecap=\"round\" stroke-linejoin=\"round\">" +
                   "<circle cx=\"12\" cy=\"12\" r=\"10\"/><line x1=\"15\" y1=\"9\" x2=\"9\" y2=\"15\"/><line x1=\"9\" y1=\"9\" x2=\"15\" y2=\"15\"/></svg>" +
                   "<h3>" + title + "</h3>" +
                   "<p>" + message + "</p></div>";
        }

        // ------------------------------------------------------------------
        //  SVG Icons   (all inline, no external files needed)
        // ------------------------------------------------------------------

        private string GetFolderSvg()
        {
            return @"<svg viewBox=""0 0 24 24"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
                <path d=""M2 6C2 4.89543 2.89543 4 4 4H9L11 7H20C21.1046 7 22 7.89543 22 9V18C22 19.1046 21.1046 20 20 20H4C2.89543 20 2 19.1046 2 18V6Z""
                      fill=""#FFC947"" stroke=""#E6A800"" stroke-width=""0.5""/>
            </svg>";
        }

        private string GetFileIconSvg(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return GenericFileSvg("#94a3b8", "?");

            switch (extension.ToLowerInvariant())
            {
                // PDF - Red
                case ".pdf":
                    return TypedFileSvg("#e74c3c", "#c0392b", "PDF");

                // Excel - Green
                case ".xls":
                case ".xlsx":
                case ".xlsm":
                case ".xlsb":
                case ".csv":
                    return TypedFileSvg("#27ae60", "#1e8449", "XLS");

                // Word - Blue
                case ".doc":
                case ".docx":
                case ".rtf":
                case ".odt":
                    return TypedFileSvg("#2980b9", "#1a5276", "DOC");

                // PowerPoint - Orange
                case ".ppt":
                case ".pptx":
                case ".pps":
                case ".ppsx":
                    return TypedFileSvg("#e67e22", "#d35400", "PPT");

                // Email - Purple
                case ".msg":
                case ".eml":
                    return EmailSvg();

                // Images - Teal
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".bmp":
                case ".svg":
                case ".webp":
                case ".tif":
                case ".tiff":
                case ".ico":
                    return ImageSvg();

                // Text - Dark gray
                case ".txt":
                case ".log":
                case ".md":
                case ".json":
                case ".xml":
                case ".htm":
                case ".html":
                case ".ini":
                case ".cfg":
                case ".yaml":
                case ".yml":
                    return TypedFileSvg("#5d6d7e", "#4a5568", "TXT");

                // Archives - Amber
                case ".zip":
                case ".rar":
                case ".7z":
                case ".gz":
                case ".tar":
                case ".bz2":
                    return ArchiveSvg();

                // Audio
                case ".mp3":
                case ".wav":
                case ".flac":
                case ".aac":
                case ".ogg":
                case ".wma":
                    return TypedFileSvg("#8e44ad", "#6c3483", "MP3");

                // Video
                case ".mp4":
                case ".avi":
                case ".mov":
                case ".mkv":
                case ".wmv":
                case ".flv":
                    return TypedFileSvg("#e74c3c", "#922b21", "VID");

                default:
                    return GenericFileSvg("#94a3b8", extension.TrimStart('.').ToUpperInvariant().Substring(0, Math.Min(3, extension.Length - 1)));
            }
        }

        /// <summary>Generic file icon with a colored label badge.</summary>
        private string TypedFileSvg(string color, string darkColor, string label)
        {
            return $@"<svg viewBox=""0 0 56 56"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
                <path d=""M14 4h20l12 12v32a4 4 0 0 1-4 4H14a4 4 0 0 1-4-4V8a4 4 0 0 1 4-4z""
                      fill=""#f8f9fa"" stroke=""#dee2e6"" stroke-width=""1.5""/>
                <path d=""M34 4l12 12H38a4 4 0 0 1-4-4V4z"" fill=""#e9ecef""/>
                <rect x=""8"" y=""34"" width=""40"" height=""16"" rx=""3"" fill=""{color}""/>
                <text x=""28"" y=""46"" text-anchor=""middle"" fill=""#fff""
                      font-family=""Arial,Helvetica,sans-serif"" font-size=""10"" font-weight=""700"">{label}</text>
            </svg>";
        }

        private string GenericFileSvg(string color, string label)
        {
            return TypedFileSvg(color, color, label);
        }

        private string EmailSvg()
        {
            return @"<svg viewBox=""0 0 56 56"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
                <path d=""M14 4h20l12 12v32a4 4 0 0 1-4 4H14a4 4 0 0 1-4-4V8a4 4 0 0 1 4-4z""
                      fill=""#f8f9fa"" stroke=""#dee2e6"" stroke-width=""1.5""/>
                <path d=""M34 4l12 12H38a4 4 0 0 1-4-4V4z"" fill=""#e9ecef""/>
                <rect x=""14"" y=""22"" width=""28"" height=""18"" rx=""2"" fill=""#8e44ad"" opacity=""0.15""/>
                <rect x=""14"" y=""22"" width=""28"" height=""18"" rx=""2"" stroke=""#8e44ad"" stroke-width=""1.5"" fill=""none""/>
                <polyline points=""14,22 28,33 42,22"" fill=""none"" stroke=""#8e44ad"" stroke-width=""1.5"" stroke-linecap=""round"" stroke-linejoin=""round""/>
            </svg>";
        }

        private string ImageSvg()
        {
            return @"<svg viewBox=""0 0 56 56"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
                <path d=""M14 4h20l12 12v32a4 4 0 0 1-4 4H14a4 4 0 0 1-4-4V8a4 4 0 0 1 4-4z""
                      fill=""#f8f9fa"" stroke=""#dee2e6"" stroke-width=""1.5""/>
                <path d=""M34 4l12 12H38a4 4 0 0 1-4-4V4z"" fill=""#e9ecef""/>
                <rect x=""14"" y=""22"" width=""28"" height=""20"" rx=""2"" fill=""#1abc9c"" opacity=""0.12""/>
                <rect x=""14"" y=""22"" width=""28"" height=""20"" rx=""2"" stroke=""#1abc9c"" stroke-width=""1.5"" fill=""none""/>
                <circle cx=""22"" cy=""29"" r=""3"" fill=""#f39c12""/>
                <polyline points=""14,42 24,34 30,38 36,30 42,36 42,42"" fill=""#1abc9c"" opacity=""0.25""/>
                <polyline points=""14,42 24,34 30,38 36,30 42,36"" fill=""none"" stroke=""#1abc9c"" stroke-width=""1.5""
                          stroke-linecap=""round"" stroke-linejoin=""round""/>
            </svg>";
        }

        private string ArchiveSvg()
        {
            return @"<svg viewBox=""0 0 56 56"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
                <path d=""M14 4h20l12 12v32a4 4 0 0 1-4 4H14a4 4 0 0 1-4-4V8a4 4 0 0 1 4-4z""
                      fill=""#f8f9fa"" stroke=""#dee2e6"" stroke-width=""1.5""/>
                <path d=""M34 4l12 12H38a4 4 0 0 1-4-4V4z"" fill=""#e9ecef""/>
                <rect x=""24"" y=""8"" width=""4"" height=""4"" fill=""#d4a017"" opacity=""0.6""/>
                <rect x=""28"" y=""12"" width=""4"" height=""4"" fill=""#d4a017"" opacity=""0.6""/>
                <rect x=""24"" y=""16"" width=""4"" height=""4"" fill=""#d4a017"" opacity=""0.6""/>
                <rect x=""28"" y=""20"" width=""4"" height=""4"" fill=""#d4a017"" opacity=""0.6""/>
                <rect x=""24"" y=""24"" width=""4"" height=""4"" fill=""#d4a017"" opacity=""0.6""/>
                <rect x=""22"" y=""30"" width=""12"" height=""10"" rx=""1.5"" stroke=""#d4a017"" stroke-width=""1.5"" fill=""#fef9e7""/>
                <line x1=""22"" y1=""34"" x2=""34"" y2=""34"" stroke=""#d4a017"" stroke-width=""1""/>
                <rect x=""8"" y=""40"" width=""40"" height=""12"" rx=""3"" fill=""#d4a017""/>
                <text x=""28"" y=""50"" text-anchor=""middle"" fill=""#fff""
                      font-family=""Arial,Helvetica,sans-serif"" font-size=""9"" font-weight=""700"">ZIP</text>
            </svg>";
        }

        // Small inline icons for buttons
        private string EyeSvg()
        {
            return @"<svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"">
                <path d=""M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z""/>
                <circle cx=""12"" cy=""12"" r=""3""/></svg>";
        }

        private string DownloadSvg()
        {
            return @"<svg viewBox=""0 0 24 24"" fill=""none"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round"">
                <path d=""M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4""/>
                <polyline points=""7 10 12 15 17 10""/>
                <line x1=""12"" y1=""15"" x2=""12"" y2=""3""/></svg>";
        }
    }
}
