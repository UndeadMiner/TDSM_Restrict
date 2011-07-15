Restrict for TDSM
=================
Restrict is a general plugin for character name registration and guest
privilege restriction.

Features
--------
 + A user registration system allowing users to submit registration
   requests in-game, that are approved by ops or from the console.
 + Password authentication of registered users upon connection using
   the server password prompt. (But passwords don't need to be unique.)
 + Automatic opping for registered operator users.
 + Restricting guests' ability to destroy/place tiles or use explosives
 + Restricting guests' ability to close/open doors
 + Fully configurable without restarting the server

Configuration
-------------

Set the servername that is used for hashing passwords, don't change it
after users have been added, because it'll invalidate old passwords.

`ro --server-id servername`

`ro -s servername`

Decide whether to allow guests in or not.

`ro --allow-guests true|false`

`ro -g true|false`

If guests are allowed in, whether to allow them to alter tiles and use
explosives.

`ro --restrict-guests true|false`

`ro -r true|false`

Their ability to open doors can also be restricted.

`ro --restrict-guests-doors true|false`

`ro -d true|false`

To check the current configuration, simply:

`ro`

To reload the user database from disk:

`ro --reload-users`

`ro -L`

### Registering users manually

By specifying the plaintext password:

`ru username -p password`

Or by giving the SHA256 hash of the string username:servername:password,
this allows registration to be done securely over forums or other public
channels.

`ru username hash`

The above commands can also be used with existing names to change their
passwords.

### Removing users

To unregister:

`ur name`

### Toggling operator status

These commands with add or remove the operator status on an existing user.

`ru -o name`

`ru name`

### In-game registration

Guests have a chat command available, that allows them to submit a request
for registration.

`/rr password`

Online ops are notified of new requests. Pending requests can be listed
with:

`rr`

To grant or deny a request, reference it by its number.

`rr --grant #`

`rr -g #`

`rr --deny #`

`rr -d #`

For the time being, requests are not persisted and vanish between restarts.

