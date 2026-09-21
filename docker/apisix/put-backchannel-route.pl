use strict;
use warnings;
use IO::Socket::INET;

my $key = $ENV{APISIX_ADMIN_KEY} // die "APISIX_ADMIN_KEY is missing\n";
open my $file, '<', '/usr/local/apisix/conf/backchannel-route.json' or die $!;
local $/;
my $body = <$file>;
close $file;

my $socket = IO::Socket::INET->new(
    PeerAddr => '127.0.0.1',
    PeerPort => 9181,
    Proto => 'tcp',
    Timeout => 3,
) or die "APISIX Admin API is not ready: $!\n";

my $path = '/apisix/admin/routes/nexusauth-sso-backchannel';
my $request = "PUT $path HTTP/1.1\r\n"
    . "Host: 127.0.0.1\r\n"
    . "X-API-KEY: $key\r\n"
    . "Content-Type: application/json\r\n"
    . "Content-Length: " . length($body) . "\r\n"
    . "Connection: close\r\n\r\n"
    . $body;
print {$socket} $request or die "Unable to send APISIX route: $!\n";
my $status = <$socket> // die "No response from APISIX Admin API\n";
close $socket;
die "APISIX route setup failed: $status" unless $status =~ m{^HTTP/1\.[01] 2\d\d\b};
print "SSO backchannel route installed.\n";
