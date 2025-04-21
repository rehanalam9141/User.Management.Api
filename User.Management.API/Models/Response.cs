namespace User.Management.API.Models;

public class Response
{
    public string? status { get; set; }
    public string? message { get; set; }
    
    public bool IsSuccess { get; set; }
}