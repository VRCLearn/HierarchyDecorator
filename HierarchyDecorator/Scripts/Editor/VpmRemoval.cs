using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;

namespace VRCLearn.HierarchyDecorator
{
    internal static class VpmRemoval
    {
        internal static Action GetRemoval()
        {
            const string manifestPath = "Packages/vpm-manifest.json";
            if (!File.Exists(manifestPath) || !File.ReadAllText(manifestPath).Contains("\"" + UpstreamMigration.PackageId + "\""))
                return null;

            Type projectType = Type.GetType("VRC.PackageManagement.Core.Types.UnityProject, vpm-core-lib", false);
            Type manifestType = projectType?.Assembly.GetType("VRC.PackageManagement.Core.Types.Packages.VPMProjectManifest");
            if (manifestType == null)
                throw new InvalidOperationException("This project needs an available VPM Resolver to migrate wooshii automatically. The old installation has been kept unchanged.");

            object manifest = manifestType.GetMethod("Load", new[] { typeof(string) })
                .Invoke(null, new object[] { Path.GetFullPath(".") });
            var locked = (IDictionary)manifestType.GetField("locked").GetValue(manifest);
            if (!locked.Contains(UpstreamMigration.PackageId))
            {
                var direct = (IDictionary)manifestType.GetField("dependencies").GetValue(manifest);
                if (direct.Contains(UpstreamMigration.PackageId))
                    throw new InvalidOperationException("The wooshii VPM records are incomplete. Resolve packages with VCC/ALCOM before migrating.");
                return null;
            }

            foreach (DictionaryEntry entry in locked)
            {
                var dependencies = entry.Value.GetType().GetField("dependencies").GetValue(entry.Value) as IDictionary;
                if (dependencies != null && dependencies.Contains(UpstreamMigration.PackageId))
                    throw new InvalidOperationException("Cannot remove wooshii while required by " + entry.Key + ".");
            }

            MethodInfo remove = projectType.GetMethods().FirstOrDefault(method =>
                method.Name == "RemoveVPMPackage" && method.ReturnType == typeof(bool) &&
                (method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(new[] { typeof(string), typeof(bool) }) ||
                 method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(new[] { typeof(string), typeof(bool), typeof(string) })));
            if (remove == null)
                throw new InvalidOperationException("The installed VPM Resolver does not provide a supported package removal API.");
            object project = Activator.CreateInstance(projectType, Path.GetFullPath("."));
            return () =>
            {
                object[] arguments = remove.GetParameters().Length == 2
                    ? new object[] { UpstreamMigration.PackageId, false }
                    : new object[] { UpstreamMigration.PackageId, false, null };
                try
                {
                    if (!(bool)remove.Invoke(project, arguments))
                        throw new IOException("VPM could not remove wooshii. Check package dependencies and file permissions; the migration has not completed.");
                }
                catch (TargetInvocationException exception)
                {
                    throw new IOException("VPM could not remove wooshii: " + exception.InnerException?.Message, exception.InnerException);
                }
                object updated = manifestType.GetMethod("Load", new[] { typeof(string) })
                    .Invoke(null, new object[] { Path.GetFullPath(".") });
                if (((IDictionary)manifestType.GetField("locked").GetValue(updated)).Contains(UpstreamMigration.PackageId) ||
                    ((IDictionary)manifestType.GetField("dependencies").GetValue(updated)).Contains(UpstreamMigration.PackageId) ||
                    Directory.Exists(Path.Combine("Packages", UpstreamMigration.PackageId)))
                    throw new IOException("VPM still contains the wooshii installation or package records.");
            };
        }
    }
}
