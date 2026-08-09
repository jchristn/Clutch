import React from 'react';
import ReactDOM from 'react-dom/client';
import './i18n'; // initialize i18next before first paint
import App from './App';
import './index.css';

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
