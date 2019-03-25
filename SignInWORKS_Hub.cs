using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using Microsoft.AspNet.SignalR.Hubs;
using Microsoft.AspNet.SignalR;
using System.Collections.ObjectModel;

using SignInWORKS.Model;
using SignInWORKS.HubController;

namespace SignInWORKS_Hub
{
    [HubName( "SignInWORKS_Hub" )]
    public class SignInWORKS_Hub : Hub
    {
        public static System.Diagnostics.EventLog EventLog { get; set; }

        // A map between the child clients and their SignalR connection Ids
        private static readonly ConcurrentDictionary<int, string> ConnectionMap = new ConcurrentDictionary<int, string>();

        public static ObservableCollection<Employee> CachedEmployeeData { get; set; }
        private static bool dataChanged = true;
        public static bool DataChanged
        {
            get { return dataChanged; }
            set { dataChanged = value; }
        }

        #region Connection Status Handlers

        /// <summary>
        /// Invoked when a client connects to the hub
        /// </summary>
        /// <returns></returns>
        public override Task OnConnected()
        {
            // Context.QueryString[ "id" ] will be null if its the server connecting.
            if ( Context.QueryString[ "id" ] == null )
            {
                // The server doesn't have an id in this case
                // in real usage you may want to authenticate the server in some way
                EventLog.WriteEntry( "Server connected" );
                return base.OnConnected();
            }
            // When a client connects, we cache the SignalR connection ID in a dictionary that maps the client ID to the connection ID
            var clientId = Convert.ToInt32( Context.QueryString[ "id" ] );
            EventLog.WriteEntry( $"Client {clientId} connected" );
            ConnectionMap[ clientId ] = Context.ConnectionId;

            Clients.Client( Context.ConnectionId ).ClientConnected();

            return base.OnConnected();
        }

        /// <summary>
        /// Invoked when a client disconnects from the hub
        /// </summary>
        /// <param name="stopCalled"></param>
        /// <returns></returns>
        public override Task OnDisconnected( bool stopCalled )
        {
            if ( Context.QueryString[ "id" ] == null )
            {
                EventLog.WriteEntry( "Server disconnected" );
                Clients.All.Shutdown();
                return base.OnDisconnected( stopCalled );
            }
            var clientId = Convert.ToInt32( Context.QueryString[ "id" ] );
            EventLog.WriteEntry( $"Client {clientId} disconnected" );
            ConnectionMap.TryRemove( clientId, out string dontCare );
            return base.OnDisconnected( stopCalled );
        }

        /// <summary>
        /// Invoked when a temporary network connection error interrupts a client's connection to the hub
        /// </summary>
        /// <returns></returns>
        public override Task OnReconnected()
        {
            if ( Context.QueryString[ "id" ] == null )
            {
                EventLog.WriteEntry( "Server reconnected" );
                return base.OnReconnected();
            }
            var clientId = Convert.ToInt32( Context.QueryString[ "id" ] );
            EventLog.WriteEntry( $"Client {clientId} reconnected" );
            ConnectionMap[ clientId ] = Context.ConnectionId;

            Clients.Client( Context.ConnectionId ).ClientReconnected();

            return base.OnReconnected();
        }

        /// <summary>
        /// ShutDown() will be called by our server to signal to the client processes that they should spin down and close themselves. 
        /// We'll only allow the server client to invoke this method - if a naughty client tries, we'll ignore it.
        /// </summary>
        public void ShutDown()
        {
            if ( Context.QueryString[ "id" ] == null )
            {
                EventLog.WriteEntry( "Server sends shutdown order" );
                Clients.All.Shutdown();
            }
            else
            {
                EventLog.WriteEntry( $"Received bad command from client {Context.QueryString[ "id" ]} : ShutDown" );
            }
        }

        #endregion

        #region Data Transfer Methods

        public ObservableCollection<Employee> GetEmployeeData()
        {
            try
            {
                if ( DataChanged )
                {
                    try
                    {
                        CachedEmployeeData = Utilities.PopulateModel();

                        DataChanged = false;
                    }
                    catch ( Exception ex )
                    {
                        EventLog.WriteEntry( $"Get GetEmployeeData {ex.ToString()}" );
                        return null;
                    }
                }
                return CachedEmployeeData;
            }
            catch ( Exception ex )
            {
                EventLog.WriteEntry( $"Get GetEmployeeData {ex.ToString()}" );
                return null;
            }
        }

        public Dictionary<int, string> GetLocationData()
        {
            Dictionary<int, string> locationData = null;

            try
            {
                locationData = Utilities.PopulateLocations();

                return locationData;
            }
            catch ( Exception ex )
            {
                EventLog.WriteEntry( $"Get GetLocationData {ex.ToString()}" );
                return null;
            }

            //Clients.Client( Context.ConnectionId ).RequestAllLocationData( locationData ?? null );
            //Clients.Client( Context.ConnectionId ).RetrieveLocationData( locationData ?? null );
        }

        public void DataUpdateFromClient( List<EmployeeStatusUpdate> employeeStatusUpdate )
        {
            if ( employeeStatusUpdate == null )
            {
                EventLog.WriteEntry( $"Change made by client {Context.ConnectionId} returned a null value." );
                return;
            }

            EventLog.WriteEntry( $"Change made by client {Context.ConnectionId}." );

            try
            {
                // update the database with the new values
                Utilities.UpdateEmployeeStatus( employeeStatusUpdate );
                DataChanged = true;
            }
            catch ( Exception ex )
            {
                EventLog.WriteEntry( "Could not update the database with the values provided." );
                return;
            }

            try
            {
                // push the new values to the other clients
                //Clients.AllExcept( Context.ConnectionId ).GetEmployeeStatusUpdate( employeeStatusUpdate );
                Clients.All.ClientDataUpdate( employeeStatusUpdate );
            }
            catch ( Exception ex )
            {
                EventLog.WriteEntry( "Failed to push the change to all clients." );
                return;
            }
        }

        #endregion

    }
}
