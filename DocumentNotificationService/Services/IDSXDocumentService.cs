using System.ServiceModel;

namespace DocumentNotificationService.Services;

/// <summary>
/// Interface for DSX Document Handling Service
/// </summary>
[ServiceContract(Namespace = "HitecLabs.DataStore.DocumentHandlingApi")]
public interface IDSXDocumentService
{
    [OperationContract]
    Task<SearchWithResultsResponse> SearchWithResultsAsync(SearchWithResultsRequest request);

    [OperationContract]
    Task<GetSearchResultsResponse> GetSearchResultsAsync(GetSearchResultsRequest request);
}

// Request/Response classes based on WSDL analysis
[System.Runtime.Serialization.DataContract]
public class SearchWithResultsRequest
{
    [System.Runtime.Serialization.DataMember]
    public SearchCriteria SearchCriteria { get; set; } = new();
    
    [System.Runtime.Serialization.DataMember]
    public int MaxResults { get; set; } = 50;
}

[System.Runtime.Serialization.DataContract]
public class SearchWithResultsResponse
{
    [System.Runtime.Serialization.DataMember]
    public DocumentSearchResult[] Results { get; set; } = Array.Empty<DocumentSearchResult>();
    
    [System.Runtime.Serialization.DataMember]
    public int TotalCount { get; set; }
    
    [System.Runtime.Serialization.DataMember]
    public string SearchId { get; set; } = string.Empty;
}

[System.Runtime.Serialization.DataContract]
public class GetSearchResultsRequest
{
    [System.Runtime.Serialization.DataMember]
    public string SearchId { get; set; } = string.Empty;
    
    [System.Runtime.Serialization.DataMember]
    public int StartIndex { get; set; }
    
    [System.Runtime.Serialization.DataMember]
    public int MaxResults { get; set; }
}

[System.Runtime.Serialization.DataContract]
public class GetSearchResultsResponse
{
    [System.Runtime.Serialization.DataMember]
    public DocumentSearchResult[] Results { get; set; } = Array.Empty<DocumentSearchResult>();
    
    [System.Runtime.Serialization.DataMember]
    public int TotalCount { get; set; }
    
    [System.Runtime.Serialization.DataMember]
    public bool HasMore { get; set; }
}

[System.Runtime.Serialization.DataContract]
public class SearchCriteria
{
    [System.Runtime.Serialization.DataMember]
    public MetadataSearchField[] MetadataFields { get; set; } = Array.Empty<MetadataSearchField>();
    
    [System.Runtime.Serialization.DataMember]
    public DateTime? FromDate { get; set; }
    
    [System.Runtime.Serialization.DataMember]
    public DateTime? ToDate { get; set; }
}

[System.Runtime.Serialization.DataContract]
public class MetadataSearchField
{
    [System.Runtime.Serialization.DataMember]
    public string FieldName { get; set; } = string.Empty;
    
    [System.Runtime.Serialization.DataMember]
    public string Value { get; set; } = string.Empty;
    
    [System.Runtime.Serialization.DataMember]
    public SearchOperation Operation { get; set; } = SearchOperation.Equals;
}

[System.Runtime.Serialization.DataContract]
public enum SearchOperation
{
    [System.Runtime.Serialization.EnumMember]
    Equals,
    [System.Runtime.Serialization.EnumMember]
    Contains,
    [System.Runtime.Serialization.EnumMember]
    StartsWith,
    [System.Runtime.Serialization.EnumMember]
    GreaterThan,
    [System.Runtime.Serialization.EnumMember]
    LessThan
}

[System.Runtime.Serialization.DataContract]
public class DocumentSearchResult
{
    [System.Runtime.Serialization.DataMember]
    public string DocumentId { get; set; } = string.Empty;
    
    [System.Runtime.Serialization.DataMember]
    public string Name { get; set; } = string.Empty;
    
    [System.Runtime.Serialization.DataMember]
    public MetadataField[] Metadata { get; set; } = Array.Empty<MetadataField>();
    
    [System.Runtime.Serialization.DataMember]
    public DateTime CreatedDate { get; set; }
}

[System.Runtime.Serialization.DataContract]
public class MetadataField
{
    [System.Runtime.Serialization.DataMember]
    public string Name { get; set; } = string.Empty;
    
    [System.Runtime.Serialization.DataMember]
    public string Value { get; set; } = string.Empty;
}