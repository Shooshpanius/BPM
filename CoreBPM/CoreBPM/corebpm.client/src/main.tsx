import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import 'bpmn-js/dist/assets/diagram-js.css'
import 'bpmn-js/dist/assets/bpmn-js.css'
import 'bpmn-js/dist/assets/bpmn-font/css/bpmn.css'
import App from './App.tsx'
import { initYandexMetrika } from './utils/yandexMetrika'

initYandexMetrika()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
