-- What a session was started from.
--
-- A browser holds its session as the HttpOnly cookie; the iOS and Android
-- apps hold the same token in the Keychain or Keystore and send it as a
-- bearer header (docs/native-app-design.md, part 3). The row is the same
-- either way; the devices screen says which kind of device it is, which the
-- user agent of an app's web view does not.

alter table sessions
    add column client text not null default 'browser'
        constraint sessions_client_check check (client in ('browser', 'ios', 'android'));

comment on column sessions.client is
    'browser (cookie), ios or android (bearer token).';
