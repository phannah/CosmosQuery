﻿using CosmosQuery.Query;
using LogicBuilder.Expressions.Utils;
using Microsoft.AspNetCore.OData.Extensions;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.OData.Edm;
using Microsoft.OData.UriParser;
using System.Reflection;

namespace CosmosQuery.Extensions;

internal static class OdataExt
{
    public static List<List<PathSegment>> GetSelects<TModel>(this ODataQueryOptions<TModel> options)
    {
        Type parentType = typeof(TModel);
        IEdmModel edmModel = options.Context.Model;

        var selects = options.GetSelectPaths<PathSelectItem>();

        if (!selects.Any())
        {
            // If there are no selects or only selects for expanded entities,
            // we need to expand the complex types on the root entity.
            return parentType.GetLiteralAndComplexSelects(edmModel);
        }

        return selects.ToList().BuildSelectPaths(parentType, edmModel, new(), new());
    }

    public static List<List<PathSegment>> GetExpansions<TModel>(this ODataQueryOptions<TModel> options)
    {
        Type parentType = typeof(TModel);
        IEdmModel edmModel = options.Context.Model;

        return options.GetSelectPaths<ExpandedNavigationSelectItem>()
            .ToList().BuildExpansionPaths(parentType, edmModel, new(), new());
    }

    
    public static string GetPropertyPath(this CountNode countNode)
    {
        switch (countNode.Source)
        {
            case CollectionNavigationNode navigationNode:
                return string.Join(".", new List<string>().GetReferencePath(navigationNode.Source, navigationNode.NavigationProperty.Name));
            case null:
                throw new ArgumentNullException(nameof(countNode));
            default:
                throw new ArgumentOutOfRangeException(nameof(countNode));
        }
    }

    public static string GetPropertyPath(this SingleValuePropertyAccessNode singleValuePropertyAccess)
            => singleValuePropertyAccess.Source switch
            {
                SingleNavigationNode navigationNode => $"{navigationNode.GetPropertyPath()}.{singleValuePropertyAccess.Property.Name}",
                SingleComplexNode complexNode => $"{complexNode.GetPropertyPath()}.{singleValuePropertyAccess.Property.Name}",
                _ => singleValuePropertyAccess.Property.Name,
            };

    public static string GetPropertyPath(this SingleNavigationNode singleNavigationNode)
            => $"{string.Join(".", new List<string>().GetReferencePath(singleNavigationNode.Source, singleNavigationNode.NavigationProperty.Name))}";

    public static string GetPropertyPath(this SingleComplexNode singleComplexNode)
        => $"{string.Join(".", new List<string>().GetReferencePath(singleComplexNode.Source, singleComplexNode.Property.Name))}";

    public static string GetPropertyPath(this CollectionComplexNode collectionComplexNode)
        => $"{string.Join(".", new List<string>().GetReferencePath(collectionComplexNode.Source, collectionComplexNode.Property.Name))}";

    public static string GetPropertyPath(this CollectionNavigationNode collectionNavigationNode)
        => $"{string.Join(".", new List<string>().GetReferencePath(collectionNavigationNode.Source, collectionNavigationNode.NavigationProperty.Name))}";

    public static List<string> GetReferencePath(this List<string> list, SingleResourceNode singleResourceNode, string propertyName)
    {
        switch (singleResourceNode)
        {
            case SingleNavigationNode sourceNode:
                list.GetReferencePath(sourceNode.Source, sourceNode.NavigationProperty.Name);
                list.Add(propertyName);
                return list;
            case SingleComplexNode complexNode:
                list.GetReferencePath(complexNode.Source, complexNode.Property.Name);
                list.Add(propertyName);
                return list;
            default:
                list.Add(propertyName);
                return list;
        }
    }

    public static void AddExpandOptionsResult(this ODataQueryOptions options)
    {
        if (options.SelectExpand is null)
            return;

        options.Request.ODataFeature().SelectExpandClause = options.SelectExpand.SelectExpandClause;
    }

    public static void AddCountOptionsResult(this ODataQueryOptions options, long longCount)
    {
        if (options.Count?.Value != true)
            return;

        options.Request.ODataFeature().TotalCount = longCount;
    }

    public static void AddNextLinkOptionsResult(this ODataQueryOptions options, int pageSize)
    {
        if (options.Request is null)
            return;

        options.Request.ODataFeature().NextLink = options.Request.GetNextPageLink(pageSize, null, null);
    }

    private static List<List<PathSegment>> AddLiteralAndComplexSelects(this List<List<PathSegment>> paths, List<PathSegment> pathSegments, IEdmModel edmModel)
    {
        PathSegment pathSegment = pathSegments.Last();
        Type memberType = pathSegment.ElementType;

        var memberSelects = memberType.GetLiteralTypeMembers()
            .Select(m => AddPathSegment(m, EdmTypeKind.Primitive, pathSegments.ToNewList()));

        var complexPaths = edmModel.GetComplexTypeSelects(memberType).Select
        (
            paths =>
            {
                paths.InsertRange(0, pathSegments.ToNewList());
                return paths;
            }
        );

        paths.AddRange(memberSelects.Concat(complexPaths));
        return paths;

        static List<PathSegment> AddPathSegment(MemberInfo member, EdmTypeKind edmTypeKind, List<PathSegment> pathSegments)
        {
            pathSegments.Add(new
            (
                member,
                member.DeclaringType!,
                member.GetMemberType().GetCurrentType(),
                edmTypeKind
            ));
            return pathSegments;
        }
    }

    private static List<List<PathSegment>> BuildExpansionPaths(
        this IReadOnlyList<ExpandedNavigationSelectItem> selectedPaths,
        Type parentType,
        IEdmModel edmModel,
        List<List<PathSegment>> paths,
        List<PathSegment> currentPath,
        in int depth = 0)
    {
        if (!selectedPaths.Any())
            return paths;

        List<PathSegment> segments = depth == 0
            ? currentPath
            : currentPath.ToNewList();

        BuildPathSegments(selectedPaths.First(), segments, depth);

        if (depth == 0 || !currentPath.Equals(segments))
            paths.Add(segments);

        foreach (var selectItem in selectedPaths.Skip(1))
            paths.Add(BuildPathSegments(selectItem, currentPath.ToNewList(), depth));

        return paths;

        List<PathSegment> BuildPathSegments(ExpandedNavigationSelectItem pathSegments, List<PathSegment> path, in int depth)
        {
            Type rootType = parentType;
            foreach (var pathSegment in pathSegments.PathToNavigationProperty)
            {
                MemberInfo member = rootType.GetMemberInfo(pathSegment.Identifier);
                Type memberType = member.GetMemberType();
                Type elementType = memberType.GetCurrentType();

                path.Add(new
                (
                    member,
                    rootType,
                    memberType,
                    pathSegment.EdmType.TypeKind,
                    pathSegment.GetFilter(pathSegments),
                    pathSegment.GetQuery(pathSegments),
                    pathSegment.GetSelects(pathSegments, elementType, edmModel)
                ));

                rootType = elementType;
            }

            pathSegments.GetSelectPaths<ExpandedNavigationSelectItem>()
                .ToList().BuildExpansionPaths(rootType, edmModel, paths, path, depth + 1);

            return path;
        }
    }

    private static List<List<PathSegment>> BuildSelectPaths(
        this IReadOnlyList<PathSelectItem> selectedPaths,
        Type parentType,
        IEdmModel edmModel,
        List<List<PathSegment>> paths,
        List<PathSegment> currentPath,
        in int depth = 0)
    {
        for (int i = 0; i < selectedPaths.Count; ++i)
        {
            List<PathSegment> segments = i == 0
                ? currentPath
                : currentPath.Take(depth).ToNewList();

            segments = BuildPathSegments(selectedPaths[i], segments, depth);

            if (depth == 0 || !segments.Equals(currentPath))
            {
                PathSegment lastSegment = segments.Last();

                if (!lastSegment.IsComplex || lastSegment.FilterOptions is not null || lastSegment.QueryOptions is not null)
                    paths.Add(segments);

                if (lastSegment.IsComplex)
                    paths.AddLiteralAndComplexSelects(segments, edmModel);
            }
        }

        return paths;

        List<PathSegment> BuildPathSegments(PathSelectItem pathSegments, List<PathSegment> path, in int depth)
        {
            Type rootType = parentType;
            foreach (var pathSegment in pathSegments.SelectedPath)
            {
                MemberInfo member = rootType.GetMemberInfo(pathSegment.Identifier);
                Type memberType = member.GetMemberType();
                Type elementType = memberType.GetCurrentType();

                path.Add(new
                (
                    member,
                    rootType,
                    memberType,
                    pathSegment.EdmType.AsElementType().TypeKind,
                    pathSegment.GetFilter(pathSegments),
                    pathSegment.GetQuery(pathSegments)
                ));

                rootType = elementType;
            }

            pathSegments.GetSelectPaths<PathSelectItem>().ToList()
                .BuildSelectPaths(rootType, edmModel, paths, path, depth + 1);

            return path;
        }
    }

    private static List<PathSegment> ToNewList(this IEnumerable<PathSegment> pathSegments) =>
        new(pathSegments.Select
        (
            p => new PathSegment
            (
                p.Member,
                p.ParentType,
                p.MemberType,
                p.EdmTypeKind
            ))
        );

    private static FilterOptions? GetFilter(this ODataPathSegment pathSegment, ExpandedNavigationSelectItem pathSegments)
    {
        if (pathSegments.PathToNavigationProperty.Last().Identifier.Equals(pathSegment.Identifier)
            && pathSegments.FilterOption is not null)
        {
            return new(pathSegments.FilterOption);
        }
        return null;
    }

    private static QueryOptions? GetQuery(this ODataPathSegment pathSegment, ExpandedNavigationSelectItem pathSegments)
    {
        if (!pathSegments.PathToNavigationProperty.Last().Identifier.Equals(pathSegment.Identifier))
            return null;

        if (pathSegments.OrderByOption is not null || pathSegments.SkipOption.HasValue || pathSegments.TopOption.HasValue)
            return new(pathSegments.OrderByOption!, (int?)pathSegments.SkipOption, (int?)pathSegments.TopOption);

        return null;
    }

    private static FilterOptions? GetFilter(this ODataPathSegment pathSegment, PathSelectItem pathSegments)
    {
        if (pathSegments.SelectedPath.Last().Identifier.Equals(pathSegment.Identifier)
            && pathSegments.FilterOption is not null)
        {
            return new(pathSegments.FilterOption);
        }

        return null;
    }

    private static QueryOptions? GetQuery(this ODataPathSegment pathSegment, PathSelectItem pathSegments)
    {
        if (!pathSegments.SelectedPath.Last().Identifier.Equals(pathSegment.Identifier))
            return null;

        if (pathSegments.OrderByOption is not null || pathSegments.SkipOption.HasValue || pathSegments.TopOption.HasValue)
            return new(pathSegments.OrderByOption!, (int?)pathSegments.SkipOption, (int?)pathSegments.TopOption);

        return null;
    }

    private static List<List<PathSegment>>? GetSelects(this ODataPathSegment pathSegment, ExpandedNavigationSelectItem pathSegments, Type parentType, IEdmModel edmModel)
    {
        if (pathSegments.PathToNavigationProperty.Last().Identifier.Equals(pathSegment.Identifier))
        {
            return pathSegments.GetSelectPaths<PathSelectItem>().ToList() switch
            {
                var selects when selects.Any() => selects.BuildSelectPaths(parentType, edmModel, new(), new()),
                _ => parentType.GetLiteralAndComplexSelects(edmModel)
            };
        }
        return null;
    }

    private static IEnumerable<TPathType> GetSelectPaths<TPathType>(this ExpandedNavigationSelectItem item) where TPathType : SelectItem =>
        item.SelectAndExpand?.SelectedItems.OfType<TPathType>() ?? Enumerable.Empty<TPathType>();

    private static IEnumerable<TPathType> GetSelectPaths<TPathType>(this PathSelectItem item) where TPathType : SelectItem =>
        item.SelectAndExpand?.SelectedItems.OfType<TPathType>() ?? Enumerable.Empty<TPathType>();

    private static IEnumerable<TPathType> GetSelectPaths<TPathType>(this ODataQueryOptions options) where TPathType : SelectItem =>
        options.SelectExpand?.SelectExpandClause?.SelectedItems.OfType<TPathType>() ?? Enumerable.Empty<TPathType>();
}
