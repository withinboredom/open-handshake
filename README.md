# The Open Handshake Project

A simple API and web app for tracking open resolvers on the Handshake network.

## API

GET https://handshake-batch.azurewebsites.net/api/all

Receive an array of resolvers and their metadata.

PUT https://handshake-batch.azurewebsites.net/api/host/{ip}

Add a new ip address to the list of resolvers that are regularly checked.

GET https://handshake-batch.azurewebsites.net/api/host/{ip}

Get the metadata for a single ip address.
