using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace VRCLearn.HierarchyDecorator
{
    internal static class UpstreamMigration
    {
        internal const string PackageId = "com.wooshii.hierarchydecorator";
        internal static bool IsRunning { get; private set; }

        private static RemoveRequest request;
        private static Action<Exception> completion;
        private static string settingsPath;
        private static List<string> assets;
        private static HashSet<string> folders;
        private static bool resolvePackages;
        private static bool imported;

        internal static void Start(string sourcePath, Action<Exception> completed)
        {
            if (IsRunning)
                throw new InvalidOperationException("A wooshii migration is already running.");

            // Validate the removal route before changing settings or deleting files.
            PackageInfo package = PackageInfo.GetAllRegisteredPackages().FirstOrDefault(item => item.name == PackageId);
            Action removeVpm = VpmRemoval.GetRemoval();
            if (removeVpm != null)
                RequireProjectPackage(Path.Combine("Packages", PackageId));
            if (package != null)
            {
                foreach (PackageInfo dependent in PackageInfo.GetAllRegisteredPackages())
                {
                    if (dependent.name != PackageId && dependent.dependencies.Any(item => item.name == PackageId))
                        throw new InvalidOperationException("Cannot remove wooshii while required by " + dependent.name + ".");
                }
                if (package.source == PackageSource.Embedded)
                    RequireProjectPackage(package.resolvedPath);
            }

            assets = UpstreamAssets.FindFiles();
            folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            settingsPath = sourcePath;
            RequireAssetPath(sourcePath);
            foreach (string path in assets)
            {
                RequireAssetPath(path);
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                while (parent != "Assets")
                {
                    folders.Add(parent);
                    parent = Path.GetDirectoryName(parent).Replace('\\', '/');
                }
            }
            if (package == null && assets.Count == 0)
                throw new InvalidOperationException("The wooshii installation could not be identified.");

            IsRunning = true;
            completion = completed;
            resolvePackages = removeVpm != null || package?.source == PackageSource.Embedded;
            imported = false;
            EditorApplication.LockReloadAssemblies();
            try
            {
                HierarchyDecorator.ImportSettings(sourcePath);
                imported = true;
                if (removeVpm != null)
                {
                    removeVpm();
                    RemovePackageReferenceOrFinish();
                }
                else if (package == null)
                {
                    Finish(null);
                }
                else if (package.source == PackageSource.Embedded)
                {
                    Directory.Delete(RequireProjectPackage(package.resolvedPath), true);
                    RemovePackageReferenceOrFinish();
                }
                else
                {
                    request = Client.Remove(PackageId);
                    EditorApplication.update += PollRemoval;
                }
            }
            catch (Exception exception)
            {
                Finish(exception);
            }
        }

        private static void RemovePackageReferenceOrFinish()
        {
            // Embedded packages can also override an existing UPM dependency.
            // Remove that reference too so resolving cannot restore the old package.
            if (File.ReadAllText("Packages/manifest.json").Contains("\"" + PackageId + "\""))
            {
                request = Client.Remove(PackageId);
                EditorApplication.update += PollRemoval;
            }
            else
            {
                Finish(null);
            }
        }

        private static void PollRemoval()
        {
            if (!request.IsCompleted)
                return;
            Finish(request.Status == StatusCode.Success ? null : new IOException(request.Error.message));
        }

        private static void Finish(Exception error)
        {
            EditorApplication.update -= PollRemoval;
            Action<Exception> completed = completion;
            try
            {
                if (error == null)
                {
                    AssetDatabase.StartAssetEditing();
                    try
                    {
                        foreach (string path in assets.Concat(new[] { settingsPath }).Distinct())
                        {
                            if (File.Exists(path) && !AssetDatabase.DeleteAsset(path))
                                throw new IOException("Cannot remove wooshii asset: " + path);
                        }
                        foreach (string folder in folders.OrderByDescending(path => path.Length))
                        {
                            if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any() &&
                                !AssetDatabase.DeleteAsset(folder))
                                throw new IOException("Cannot remove empty wooshii folder: " + folder);
                        }
                    }
                    finally
                    {
                        AssetDatabase.StopAssetEditing();
                    }
                    AssetDatabase.Refresh();
                    EditorApplication.RepaintHierarchyWindow();
                }
            }
            catch (Exception exception)
            {
                error = exception;
            }
            finally
            {
                IsRunning = false;
                request = null;
                completion = null;
                EditorApplication.UnlockReloadAssemblies();
            }
            if (resolvePackages)
                EditorApplication.delayCall += ResolvePackages;
            if (error != null && imported)
                error = new IOException("Settings were imported, but wooshii cleanup did not finish: " + error.Message, error);
            try
            {
                completed?.Invoke(error);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void ResolvePackages()
        {
            Client.Resolve();
            AssetDatabase.Refresh();
        }

        private static void RequireAssetPath(string path)
        {
            string full = Path.GetFullPath(path);
            if (!full.StartsWith(Path.GetFullPath("Assets") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only project Assets can be removed: " + path);
            RejectLinkedPath(full);
        }

        private static string RequireProjectPackage(string path)
        {
            string full = Path.GetFullPath(path);
            if (full != Path.GetFullPath(Path.Combine("Packages", PackageId)))
                throw new InvalidOperationException("Unexpected embedded wooshii package path: " + path);
            RejectLinkedPath(full);
            return full;
        }

        private static void RejectLinkedPath(string path)
        {
            string root = Path.GetFullPath(".");
            while (path.Length > root.Length)
            {
                if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("Cannot automatically remove a linked installation: " + path);
                path = Path.GetDirectoryName(path);
            }
        }
    }
}
