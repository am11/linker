﻿using System;
using System.Reflection.Runtime.TypeParsing;
using Mono.Cecil;

namespace Mono.Linker
{
	internal class TypeNameResolver
	{
		readonly LinkContext _context;

		public TypeNameResolver (LinkContext context)
		{
			_context = context;
		}

		public TypeReference ResolveTypeName (string typeNameString)
		{
			if (string.IsNullOrEmpty (typeNameString))
				return null;

			TypeName parsedTypeName;
			try {
				parsedTypeName = TypeParser.ParseTypeName (typeNameString);
			} catch (ArgumentException) {
				return null;
			} catch (System.IO.FileLoadException) {
				return null;
			}

			if (parsedTypeName is AssemblyQualifiedTypeName assemblyQualifiedTypeName) {
				return ResolveTypeName (null, assemblyQualifiedTypeName);
			}

			foreach (var assemblyDefiniton in _context.GetAssemblies ()) {
				var foundType = ResolveTypeName (assemblyDefiniton, parsedTypeName);
				if (foundType != null)
					return foundType;
			}

			return null;
		}

		public TypeReference ResolveTypeName (AssemblyDefinition assembly, string typeNameString)
		{
			return ResolveTypeName (assembly, TypeParser.ParseTypeName (typeNameString));
		}

		TypeReference ResolveTypeName (AssemblyDefinition assembly, TypeName typeName)
		{
			if (typeName is AssemblyQualifiedTypeName assemblyQualifiedTypeName) {
				// In this case we ignore the assembly parameter since the type name has assembly in it
				var assemblyFromName = _context.GetLoadedAssembly (assemblyQualifiedTypeName.AssemblyName.Name);
				return ResolveTypeName (assemblyFromName, assemblyQualifiedTypeName.TypeName);
			}

			if (assembly == null || typeName == null)
				return null;

			if (typeName is ConstructedGenericTypeName constructedGenericTypeName) {
				var genericTypeRef = ResolveTypeName (assembly, constructedGenericTypeName.GenericType);
				if (genericTypeRef == null)
					return null;

				TypeDefinition genericType = genericTypeRef.Resolve ();
				var genericInstanceType = new GenericInstanceType (genericType);
				foreach (var arg in constructedGenericTypeName.GenericArguments) {
					var genericArgument = ResolveTypeName (assembly, arg);
					if (genericArgument == null)
						return null;

					genericInstanceType.GenericArguments.Add (genericArgument);
				}

				return genericInstanceType;
			} else if (typeName is HasElementTypeName elementTypeName) {
				var elementType = ResolveTypeName (assembly, elementTypeName.ElementTypeName);
				if (elementType == null)
					return null;

				return typeName switch
				{
					ArrayTypeName _ => new ArrayType (elementType),
					MultiDimArrayTypeName multiDimArrayTypeName => new ArrayType (elementType, multiDimArrayTypeName.Rank),
					ByRefTypeName _ => new ByReferenceType (elementType),
					PointerTypeName _ => new PointerType (elementType),
					_ => elementType
				};
			}

			return assembly.MainModule.GetType (typeName.ToString ());
		}
	}
}