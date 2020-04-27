import { Component, OnInit } from '@angular/core';
import { AuthService } from '../core/auth-service.component';
import { Router } from '@angular/router';

@Component({
  selector: 'app-signin-callback',
  // this callback happens so quickly that we don't want to show anything here
  template: `<div></div>`
})

export class SigninRedirectCallbackComponent implements OnInit {

  constructor(
      private authService: AuthService,
      private router: Router
  ) { }

  ngOnInit() {
    this.authService.completeLogin().then(user => {
      // when login complete, go back to home
      this.router.navigate(['/'], { replaceUrl: true });
    })
  }
}
