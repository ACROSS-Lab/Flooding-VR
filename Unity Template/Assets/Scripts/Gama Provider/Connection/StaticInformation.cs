using System.Net;

public static class StaticInformation
{
    public static string endOfGame { get; set; }
    private static string connectionId;

    public static string getId() {

        if (connectionId == null || connectionId.Length == 0)
        {
            string hostName = Dns.GetHostName(); // Retrive the Name of HOST
            string myIP = Dns.GetHostByName(hostName).AddressList[0].MapToIPv4().ToString();

             string lastIP = myIP.Contains(".") ? myIP.Split(".")[3] : "0";
            connectionId =  "Player_" + lastIP;// + lastIP;
        }
        return connectionId;
    }
}