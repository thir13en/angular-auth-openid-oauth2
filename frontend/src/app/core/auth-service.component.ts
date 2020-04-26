import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

import { UserManager, User, UserManagerSettings } from 'oidc-client';
import { Subject } from 'rxjs';

import { Constants } from '../constants';
import { AuthContext } from '../model/auth-context';


@Injectable()
export class AuthService {
  // instances of utils imported by oidc-client
  private userManager: UserManager;
  private user: User;

  private loginChangedSubject = new Subject<boolean>();

  loginChanged = this.loginChangedSubject.asObservable();
  authContext: AuthContext;

  constructor(private _httpClient: HttpClient) {
    // settings to configure the user manager
    const stsSettings: UserManagerSettings = {
      authority: Constants.stsAuthority,
      client_id: Constants.clientId,
      redirect_uri: `${Constants.clientRoot}signin-callback`,
      scope: 'openid profile projects-api',
      response_type: 'code',
      post_logout_redirect_uri: `${Constants.clientRoot}signout-callback`,
      automaticSilentRenew: true,
      silent_redirect_uri: `${Constants.clientRoot}assets/silent-callback.html`
      // metadata: {
      //   issuer: `${Constants.stsAuthority}`,
      //   authorization_endpoint: `${Constants.stsAuthority}authorize?audience=projects-api`,
      //   jwks_uri: `${Constants.stsAuthority}.well-known/jwks.json`,
      //   token_endpoint: `${Constants.stsAuthority}oauth/token`,
      //   userinfo_endpoint: `${Constants.stsAuthority}userinfo`,
      //   end_session_endpoint: `${Constants.stsAuthority}v2/logout?client_id=${Constants.clientId}&returnTo=${encodeURI(Constants.clientRoot)}signout-callback`
      // }
    };
    this.userManager = new UserManager(stsSettings);
    this.userManager.events.addAccessTokenExpired(_ => {
      this.loginChangedSubject.next(false);
    });
    this.userManager.events.addUserLoaded(user => {
      if (this.user !== user) {
        this.user = user;
        this.loadSecurityContext();
        this.loginChangedSubject.next(!!user && !user.expired);
      }
    });
  }

  login() {
    return this.userManager.signinRedirect();
  }

  isLoggedIn(): Promise<boolean> {
    return this.userManager.getUser().then(user => {
      const userCurrent = !!user && !user.expired;
      if (this.user !== user) {
        this.loginChangedSubject.next(userCurrent);
      }
      if (userCurrent && !this.authContext) {
        this.loadSecurityContext();
      }
      this.user = user;
      return userCurrent;
    });
  }

  completeLogin() {
    return this.userManager.signinRedirectCallback().then(user => {
      this.user = user;
      this.loginChangedSubject.next(!!user && !user.expired);
      return user;
    });
  }

  logout() {
    this.userManager.signoutRedirect();
  }

  completeLogout() {
    this.user = null;
    this.loginChangedSubject.next(false);
    return this.userManager.signoutRedirectCallback();
  }

  getAccessToken() {
    return this.userManager.getUser().then(user => {
      if (!!user && !user.expired) {
        return user.access_token;
      }
      else {
        return null;
      }
    });
  }

  loadSecurityContext() {
    this._httpClient
      .get<AuthContext>(`${Constants.apiRoot}Projects/AuthContext`)
      .subscribe(
        context => {
          this.authContext = new AuthContext();
          this.authContext.claims = context.claims;
          this.authContext.userProfile = context.userProfile;
        },
        error => console.error(error)
      );
  }

}
