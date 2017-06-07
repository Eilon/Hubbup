// *** Global Dependencies ***
// These are things that need to be imported, but aren't used by any particular thing in the app code
// things like jQuery and bootstrap, as well as the CSS files (for WebPack to pull in)

// Note: WebPack will pull the CSS out and put it in a separate css file in the output dir.
import './app.css';

// jQuery (required by Bootstrap)
import 'jquery/src/jquery';

// Bootstrap
import 'bootstrap/dist/js/npm';
import 'bootstrap/dist/css/bootstrap.css';

// Promise Polyfill
import 'promise-polyfill/promise';

// Fetch Polyfill
import 'whatwg-fetch/fetch';

// *** End Global Dependencies ***
