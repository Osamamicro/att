window.authStorage = {
  setAuthToken: function (token) {
    try {
      localStorage.setItem('auth_token', token || '');
      return !!localStorage.getItem('auth_token');
    } catch {
      return false;
    }
  },

  setAuthTokenSession: function (token) {
    try {
      sessionStorage.setItem('auth_token', token || '');
      return !!sessionStorage.getItem('auth_token');
    } catch {
      return false;
    }
  },

  getAuthToken: function () {
    try {
      return localStorage.getItem('auth_token') || sessionStorage.getItem('auth_token') || '';
    } catch {
      return '';
    }
  },

  setPersonNumber: function (value) {
    try {
      localStorage.setItem('person_number', String(value ?? ''));
      return true;
    } catch {
      return false;
    }
  },

  getPersonNumber: function () {
    try {
      return localStorage.getItem('person_number') || '';
    } catch {
      return '';
    }
  },

  clearAuth: function () {
    try {
      localStorage.removeItem('auth_token');
      localStorage.removeItem('person_number');
      sessionStorage.removeItem('auth_token');
      return true;
    } catch {
      return false;
    }
  }
};
